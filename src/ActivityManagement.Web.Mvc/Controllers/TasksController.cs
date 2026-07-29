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
        private readonly ActivityManagement.Responsibilities.ISubCategoryResponsibilityAppService _subCatRespAppService;
        private readonly IWebHostEnvironment _env;

        public TasksController(
            ITaskItemAppService taskAppService,
            IEmployeeAppService employeeAppService,
            IProjectAppService projectAppService,
            ICategoryAppService categoryAppService,
            ITeamAppService teamAppService,
            ActivityManagement.Responsibilities.ISubCategoryResponsibilityAppService subCatRespAppService,
            IWebHostEnvironment env)
        {
            _taskAppService = taskAppService;
            _employeeAppService = employeeAppService;
            _projectAppService = projectAppService;
            _categoryAppService = categoryAppService;
            _teamAppService = teamAppService;
            _subCatRespAppService = subCatRespAppService;
            _env = env;
        }

        // Alt kategori → (asıl, yedek) sorumlu haritası (sorumluluk matrisi). Görev oluştururken 1./2. sorumlu ön-seçimi.
        private async Task<System.Collections.Generic.Dictionary<long, long?[]>> BuildSubCatRespMapAsync()
        {
            var map = new System.Collections.Generic.Dictionary<long, long?[]>(); // [0]=asıl, [1]=yedek
            try
            {
                foreach (var r in await _subCatRespAppService.GetAllAsync())
                {
                    if (!map.TryGetValue(r.SubCategoryId, out var pair)) { pair = new long?[2]; map[r.SubCategoryId] = pair; }
                    if (r.ResponsibilityType == ActivityManagement.Entities.ResponsibilityType.Primary) pair[0] = r.EmployeeId;
                    else pair[1] = r.EmployeeId;
                }
            }
            catch { }
            return map;
        }

        // Görevler: sadece Admin. Sol kategori/alt kategori ağacı, sağda seçilen (alt)kategorinin görevleri.
        // Admin "kendi" (Sistem Yöneticisi) modunda mı? (login-as ile başkasına geçmemiş)
        private bool IsAdminSelfMode()
        {
            if (!User.IsInRole("Admin")) return false;
            var own = User.FindFirst("AdminOwnEmployeeId")?.Value;
            var emp = User.FindFirst("EmployeeId")?.Value;
            return string.IsNullOrEmpty(emp) || string.IsNullOrEmpty(own) || emp == own;
        }

        public async Task<IActionResult> Index()
        {
            // Sistem Yöneticisi (admin kendi kimliği) → kategorilerdeki görevler listesi.
            // Login-as ile başka kişiye geçilmişse veya normal kullanıcı → kendi "Görevlerim" ekranı.
            if (!IsAdminSelfMode())
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
            // Sorumluluk matrisi (alt kategori → asıl/yedek sorumlu) — 1./2. sorumlu ön-seçimi (view'da JS + burada sunucu)
            var respMap = await BuildSubCatRespMapAsync();
            ViewBag.SubCatResp = respMap;
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

            // Alt kategori tree'den seçili geldiyse (proje değil): sorumluluk matrisinden 1./2. sorumluyu ön-doldur
            if (!projectId.HasValue && dto.SubCategoryId.HasValue && respMap.TryGetValue(dto.SubCategoryId.Value, out var pair))
            {
                dto.AssignedEmployeeId = dto.AssignedEmployeeId ?? pair[0];
                dto.SecondaryEmployeeId = dto.SecondaryEmployeeId ?? pair[1];
            }

            // Görev grubu ön-seçimi: hedef kişinin (atanan ya da giriş yapan) birimi otomatik seçili gelsin
            if (string.IsNullOrWhiteSpace(dto.GroupName))
            {
                var targetId = dto.AssignedEmployeeId ?? CurrentEmployeeId();
                if (targetId.HasValue)
                {
                    try { dto.GroupName = (await _employeeAppService.GetAsync(targetId.Value))?.Department; } catch { }
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
        [ValidateAntiForgeryToken]
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
                // İzin nedeniyle yeniden atama olduysa kullanıcıyı bilgilendir
                if (created != null && !string.IsNullOrEmpty(created.AssignmentNote))
                    TempData["Uyari"] = created.AssignmentNote;
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            await _taskAppService.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(long id, Entities.TaskStatus status, int percentage)
        {
            bool ajax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            try
            {
                await _taskAppService.UpdateStatusAsync(id, status, percentage);
                if (ajax) return Ok(new { success = true });
                return RedirectToAction("Detail", new { id });
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                // Ör. "boş görev Tamamlandı yapılamaz" → board'da kırmızı toast (düz metin, ABP sarmasın), formda uyarı
                if (ajax) return new ContentResult { StatusCode = 400, Content = ex.Message, ContentType = "text/plain; charset=utf-8" };
                TempData["Uyari"] = ex.Message;
                return RedirectToAction("Detail", new { id });
            }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Tasks/UpdateStatus");
                if (ajax) return new ContentResult { StatusCode = 500, Content = "Durum güncellenemedi.", ContentType = "text/plain; charset=utf-8" };
                TempData["Uyari"] = "Durum güncellenemedi.";
                return RedirectToAction("Detail", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52428800)] // 50 MB
        public async Task<IActionResult> AddComment(long taskId, string comment, bool isInternal = false, List<IFormFile> files = null, decimal? hoursSpent = null, string effortDate = null)
        {
            try
            {
                bool hasComment = !string.IsNullOrWhiteSpace(comment);
                bool hasFiles = files != null && files.Any(f => f != null && f.Length > 0);
                bool hasEffort = hoursSpent.HasValue && hoursSpent.Value > 0;

                if (!hasComment && !hasFiles && !hasEffort)
                {
                    TempData["Uyari"] = "Yapılan işi (yorum), dosya veya efor süresini girmelisiniz.";
                    return RedirectToAction("Detail", new { id = taskId });
                }

                // GÜVENLİK: yalnız güvenli dosya türleri (html/svg/js/exe reddedilir — depolanmış XSS önlenir)
                if (hasFiles && files.Any(f => f != null && f.Length > 0 && !ActivityManagement.Web.Helpers.UploadValidator.IsAllowed(f.FileName)))
                {
                    TempData["Uyari"] = "İzin verilmeyen dosya türü. İzinli türler: " + ActivityManagement.Web.Helpers.UploadValidator.AllowedListText();
                    return RedirectToAction("Detail", new { id = taskId });
                }

                // Yorum/not (ve varsa dosya) — yalnız içerik varsa oluştur
                long commentId = 0;
                if (hasComment || hasFiles)
                    commentId = await _taskAppService.AddCommentAsync(taskId, comment ?? "", isInternal);

                if (hasFiles)
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

                // Efor süresi girildiyse: yapılan iş (yorumun düz metni) açıklamasıyla efor kaydet
                if (hasEffort)
                {
                    var desc = StripHtml(comment);
                    // Efor girilen tarih (form) — boş/geçersizse bugün. Aynı göreve farklı günler için ayrı efor girilebilir.
                    var effDate = DateTime.TryParse(effortDate, out var ed) ? ed.Date : DateTime.Today;
                    await _taskAppService.LogEffortAsync(new ActivityManagement.Activities.Dto.CreateActivityLogDto
                    {
                        TaskItemId = taskId,
                        HoursSpent = hoursSpent.Value,
                        Description = string.IsNullOrWhiteSpace(desc) ? "Görev eforu" : desc,
                        ActivityType = "Görev",
                        ActivityDate = effDate
                    });
                    TempData["Success"] = $"Kaydedildi ({hoursSpent.Value:0.##} saat efor işlendi).";
                }
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Tasks/AddComment"); TempData["Uyari"] = "Kaydedilirken bir sorun oluştu."; }
            return RedirectToAction("Detail", new { id = taskId });
        }

        // Rich text (HTML) yorumu efor açıklaması için düz metne indirger.
        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
            return text.Length > 400 ? text.Substring(0, 400) : text;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(long commentId, long taskId)
        {
            try { await _taskAppService.DeleteCommentAsync(commentId); TempData["Success"] = "Yorum silindi."; }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Tasks/DeleteComment"); TempData["Uyari"] = "Yorum silinemedi."; }
            return RedirectToAction("Detail", new { id = taskId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
