using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Authorization;
using ActivityManagement.Categories;
using ActivityManagement.Categories.Dto;
using ActivityManagement.Employees;
using ActivityManagement.Projects;
using ActivityManagement.Tasks;
using ActivityManagement.Tasks.Dto;
using ActivityManagement.Teams;

namespace ActivityManagement.Web.Controllers
{
    public class TasksController : ActivityManagementControllerBase
    {
        private readonly ITaskItemAppService _taskAppService;
        private readonly IEmployeeAppService _employeeAppService;
        private readonly IProjectAppService _projectAppService;
        private readonly ICategoryAppService _categoryAppService;
        private readonly ITeamAppService _teamAppService;
        private readonly IWebHostEnvironment _env;

        public TasksController(
            ITaskItemAppService taskAppService,
            IEmployeeAppService employeeAppService,
            IProjectAppService projectAppService,
            ICategoryAppService categoryAppService,
            ITeamAppService teamAppService,
            IWebHostEnvironment env)
        {
            _taskAppService = taskAppService;
            _employeeAppService = employeeAppService;
            _projectAppService = projectAppService;
            _categoryAppService = categoryAppService;
            _teamAppService = teamAppService;
            _env = env;
        }

        // Görevler: sadece Admin. Sol kategori/alt kategori ağacı, sağda seçilen (alt)kategorinin görevleri.
        public async Task<IActionResult> Index()
        {
            // Admin olmayan bu (admin) listeyi görmez → kendi görevlerine gider (erişim reddi değil).
            if (!User.IsInRole("Admin"))
                return RedirectToAction("MyTasks");
            var g = EnsurePageAccess("Tasks"); if (g != null) return g;
            ViewBag.Categories = await _categoryAppService.GetAllAsync(onlyActive: true);
            return View();
        }

        // Kanban panosu
        public IActionResult Board()
        {
            var g = EnsurePageAccess("Board"); if (g != null) return g;
            return View();
        }

        // Gantt görünümü (MS Project tarzı): ekip görevleri zaman ekseninde
        public IActionResult Gantt()
        {
            var g = EnsurePageAccess("Board"); if (g != null) return g;
            return View();
        }

        // Görevlerim: sol kategori TreeView filtresi, orta aktif görev kartları, sağ zaman çizelgesi
        public async Task<IActionResult> MyTasks()
        {
            var g = EnsurePageAccess("MyTasks"); if (g != null) return g;
            var myEmpId = CurrentEmployeeId();
            if (!myEmpId.HasValue)
            {
                // Personel kaydı olmayan hesap (ör. bağlanmamış admin) → dostça yönlendirme (patlamaz)
                TempData["Uyari"] = "Bu hesabın personel kaydı yok; kişisel görev listesi görüntülenemiyor.";
                return Redirect(User.IsInRole("Admin") ? "/Tasks" : "/");
            }
            ViewBag.CurrentEmployeeId = myEmpId.Value;
            ViewBag.Categories = await _categoryAppService.GetAllAsync(onlyActive: true);
            var tasks = await _taskAppService.GetEmployeeTasksAsync(myEmpId.Value);
            return View(tasks.Items);
        }

        public async Task<IActionResult> Detail(long id)
        {
            try
            {
                var task = await _taskAppService.GetAsync(id);
                if (task == null)
                {
                    TempData["Uyari"] = "Görev bulunamadı.";
                    return RedirectToAction("MyTasks");
                }
                return View(task);
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Uyari"] = ex.Message;
                return RedirectToAction("MyTasks");
            }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, $"Tasks/Detail/{id}");
                TempData["Uyari"] = "Görev detayı açılırken bir hata oluştu, kayıt alındı.";
                return RedirectToAction("MyTasks");
            }
        }

        // Rol bazlı GÖRÜNÜRLÜK: Admin tüm görevler; TakımLideri ve Uzman kendi TAKIMININ görevlerini görür
        // (uzman, takımdaki kişilere atanan görevleri de görebilir). İşlem (durum/düzenleme) yetkisi ayrıca
        // "kendine ait" kuralıyla (CanEdit) sınırlıdır. TeamId filtresi kapsam dışı sızmayı engeller.
        private async Task<long?> ApplyRoleScopeAsync(GetTasksInput input)
        {
            if (User.IsInRole("Admin")) return null;
            var empId = CurrentEmployeeId();
            var myTeamId = empId.HasValue ? (await _employeeAppService.GetAsync(empId.Value)).TeamId : null;
            input.TeamId = myTeamId;
            return myTeamId;
        }

        // Görev Sorgula: tüm durumlar + tarih/kategori/personel/proje/takım/SLA + rol bazlı filtre, Excel export
        public async Task<IActionResult> Completed(GetTasksInput input)
        {
            var g = EnsurePageAccess("TaskQuery"); if (g != null) return g;
            input.MaxResultCount = input.MaxResultCount > 0 ? input.MaxResultCount : 200;
            var lockedTeamId = await ApplyRoleScopeAsync(input);
            ViewBag.IsAdmin = User.IsInRole("Admin");
            ViewBag.IsManager = IsManager();
            ViewBag.CurrentEmployeeId = CurrentEmployeeId();
            ViewBag.LockedTeamId = lockedTeamId;

            var result = await _taskAppService.GetAllAsync(input);
            await LoadCompletedFilterViewBagsAsync();
            ViewBag.Input = input;
            return View(result.Items.ToList());
        }

        public async Task<IActionResult> ExportCompletedExcel(GetTasksInput input)
        {
            input.MaxResultCount = 10000;
            await ApplyRoleScopeAsync(input);
            var result = await _taskAppService.GetAllAsync(input);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Görev Sorgu");
            var headers = new[] { "Başlık", "Kategori", "Alt Kategori", "Proje", "Takım", "1. Sorumlu", "2. Sorumlu", "Öncelik", "Son Tarih", "Tamamlanma Tarihi", "SLA Durumu" };
            for (int i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];

            int row = 2;
            foreach (var t in result.Items)
            {
                sheet.Cell(row, 1).Value = t.Title;
                sheet.Cell(row, 2).Value = t.CategoryName;
                sheet.Cell(row, 3).Value = t.SubCategoryName;
                sheet.Cell(row, 4).Value = t.ProjectName;
                sheet.Cell(row, 5).Value = t.TeamName;
                sheet.Cell(row, 6).Value = t.AssignedEmployeeName;
                sheet.Cell(row, 7).Value = t.SecondaryEmployeeName;
                sheet.Cell(row, 8).Value = t.PriorityText;
                sheet.Cell(row, 9).Value = t.DueDate?.ToString("dd.MM.yyyy") ?? "";
                sheet.Cell(row, 10).Value = t.CompletedDate?.ToString("dd.MM.yyyy") ?? "";
                sheet.Cell(row, 11).Value = t.CompletedOnTime == null ? "" : (t.CompletedOnTime.Value ? "Zamanında" : "Gecikmeli");
                row++;
            }
            sheet.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            var fileName = $"GorevSorgu_{DateTime.Today:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private async Task LoadCompletedFilterViewBagsAsync()
        {
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
            ViewBag.Categories = await _categoryAppService.GetAllAsync();
            ViewBag.Teams = await _teamAppService.GetAllAsync();
        }

        private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("TakımLideri");

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        // Tüm aktif kategoriler herkese gösterilir (görev = ana kategori + alt kategori + atanan kişi).
        private async Task LoadCategoriesViewBagAsync()
        {
            ViewBag.Categories = await _categoryAppService.GetAllAsync(onlyActive: true);
        }

        public async Task<IActionResult> Create(long? projectId, long? assignedEmployeeId, long? categoryId, long? subCategoryId)
        {
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
            await LoadCategoriesViewBagAsync();
            // Görevlerim ağacında seçili kategori/alt kategori forma otomatik gelsin
            ViewBag.PreCategoryId = categoryId;
            var dto = new CreateUpdateTaskItemDto
            {
                ProjectId = projectId,
                AssignedEmployeeId = assignedEmployeeId,
                SubCategoryId = subCategoryId,
                StartDate = DateTime.Today,          // başlangıç tarihi bugün; saat boş = tüm gün
                DueDate = DateTime.Now.AddDays(1),   // V4: son teslim varsayılan "yarın"
                PriorityScore = 5,                   // V4: önem derecesi varsayılan 5 (orta)
                EstimatedHours = 0                   // V4: süre zorunlu değil
            };

            // Proje seçiliyse: kategori + sorumlular projeden gelir ve kategori kilitlenir
            if (projectId.HasValue)
            {
                var proj = await _projectAppService.GetAsync(projectId.Value);
                if (proj != null)
                {
                    dto.SubCategoryId = proj.SubCategoryId ?? dto.SubCategoryId;
                    dto.AssignedEmployeeId = dto.AssignedEmployeeId ?? proj.PrimaryResponsibleId;
                    dto.SecondaryEmployeeId = proj.SecondaryResponsibleId;
                    ViewBag.PreCategoryId = proj.CategoryId; // ana kategori ön-seçim (boşsa view alt kategoriden türetir)
                    ViewBag.FromProject = true;
                    ViewBag.ProjectName = proj.Name;
                }
            }
            return View(dto);
        }

        // Ayrı tarih (yyyy-MM-dd) + opsiyonel saat (HH:mm) → StartDate. Saat boşsa gün başı (00:00 = tüm gün).
        private static DateTime? CombineStartDate(string datePart, string timePart)
        {
            if (string.IsNullOrWhiteSpace(datePart)) datePart = DateTime.Today.ToString("yyyy-MM-dd");
            if (!DateTime.TryParse(datePart, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                return null;
            d = d.Date;
            if (!string.IsNullOrWhiteSpace(timePart) && TimeSpan.TryParse(timePart, System.Globalization.CultureInfo.InvariantCulture, out var t))
                d = d.Add(t);
            return d;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUpdateTaskItemDto input, string startDatePart, string startTimePart)
        {
            input.StartDate = CombineStartDate(startDatePart, startTimePart);
            if (!ModelState.IsValid)
            {
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                await LoadCategoriesViewBagAsync();
                return View(input);
            }
            try
            {
                var created = await _taskAppService.CreateAsync(input);
                // Uzman'ın kendine açtığı görev onaya düşer → "yetkiniz yok" değil, dostça onay bilgisi
                if (created != null && created.ApprovalStatus == ActivityManagement.Entities.TaskApprovalStatus.Beklemede)
                    TempData["Success"] = "Görev oluşturuldu ve onaya gönderildi. Takım lideriniz onayladığında aktifleşecek.";
                else
                    TempData["Success"] = "Görev oluşturuldu.";
                // Admin admin listesine, diğerleri kendi görevlerine döner (erişebildikleri sayfa)
                return User.IsInRole("Admin") ? RedirectToAction("Index") : RedirectToAction("MyTasks");
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                await LoadCategoriesViewBagAsync();
                return View(input);
            }
        }

        public async Task<IActionResult> Edit(long id)
        {
            var task = await _taskAppService.GetAsync(id);
            var myEmpId = CurrentEmployeeId();
            if (!IsManager() && !(task.AssignedEmployeeId.HasValue && myEmpId.HasValue && task.AssignedEmployeeId == myEmpId))
                return AccessDeniedRedirect();
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
            await LoadCategoriesViewBagAsync();
            return View(ObjectMapper.Map<CreateUpdateTaskItemDto>(task));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreateUpdateTaskItemDto input, string startDatePart, string startTimePart)
        {
            input.StartDate = CombineStartDate(startDatePart, startTimePart);
            if (!ModelState.IsValid)
            {
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                await LoadCategoriesViewBagAsync();
                return View(input);
            }
            try
            {
                await _taskAppService.UpdateAsync(input);
                return RedirectToAction("Index");
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                await LoadCategoriesViewBagAsync();
                return View(input);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(long id)
        {
            await _taskAppService.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(long id, Entities.TaskStatus status, int percentage)
        {
            await _taskAppService.UpdateStatusAsync(id, status, percentage);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Ok(new { success = true });
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        [RequestSizeLimit(52428800)] // 50 MB
        public async Task<IActionResult> AddComment(long taskId, string comment, bool isInternal = false, List<IFormFile> files = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(comment) && (files == null || files.Count == 0))
                {
                    TempData["Uyari"] = "Yorum veya dosya girmelisiniz.";
                    return RedirectToAction("Detail", new { id = taskId });
                }

                var commentId = await _taskAppService.AddCommentAsync(taskId, comment ?? "", isInternal);

                if (files != null && files.Count > 0)
                {
                    var relDir = $"/uploads/tasks/{taskId}";
                    var absDir = Path.Combine(_env.WebRootPath, "uploads", "tasks", taskId.ToString());
                    Directory.CreateDirectory(absDir);
                    foreach (var f in files.Where(f => f != null && f.Length > 0))
                    {
                        var safeName = Path.GetFileName(f.FileName);
                        var stored = $"{Guid.NewGuid():N}_{safeName}";
                        var abs = Path.Combine(absDir, stored);
                        using (var fs = new FileStream(abs, FileMode.Create))
                            await f.CopyToAsync(fs);
                        await _taskAppService.AddAttachmentAsync(taskId, commentId, safeName, $"{relDir}/{stored}", f.Length, f.ContentType ?? "application/octet-stream");
                    }
                }
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Detail", new { id = taskId });
        }

        [HttpPost]
        public async Task<IActionResult> Approve(long id)
        {
            try
            {
                await _taskAppService.SetApprovalAsync(id, Entities.TaskApprovalStatus.Onaylandi);
                TempData["Success"] = "Görev onaylandı.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Reject(long id)
        {
            try
            {
                await _taskAppService.SetApprovalAsync(id, Entities.TaskApprovalStatus.Reddedildi);
                TempData["Success"] = "Görev reddedildi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Detail", new { id });
        }
    }
}
