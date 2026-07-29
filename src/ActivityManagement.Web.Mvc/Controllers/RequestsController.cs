using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Employees;
using ActivityManagement.Entities;
using ActivityManagement.Projects;
using ActivityManagement.ServiceRequests;
using ActivityManagement.ServiceRequests.Dto;

namespace ActivityManagement.Web.Controllers
{
    // Talepler: psm.tdv.org (Sunucu Kurulum) + destek.cmit.com.tr (Dış Destek). Eforlu iş olarak yönetilir.
    public class RequestsController : ActivityManagementControllerBase
    {
        private readonly IServiceRequestAppService _requestAppService;
        private readonly IEmployeeAppService _employeeAppService;
        private readonly IProjectAppService _projectAppService;
        private readonly ActivityManagement.Activities.IActivityTypeAppService _activityTypeAppService;

        public RequestsController(
            IServiceRequestAppService requestAppService,
            IEmployeeAppService employeeAppService,
            IProjectAppService projectAppService,
            ActivityManagement.Activities.IActivityTypeAppService activityTypeAppService)
        {
            _requestAppService = requestAppService;
            _employeeAppService = employeeAppService;
            _projectAppService = projectAppService;
            _activityTypeAppService = activityTypeAppService;
        }

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }
        private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("TakımLideri");

        // Talep Sorgula: TÜM talepler (arşiv/kapalı+eforlu dahil) — kaynak/durum/kişi/metin filtreli, açık+kapalı aynı ekranda.
        // Görünürlük kapsamı serviste uygulanır (Admin/Manager tümü, diğerleri kendine atanan).
        public async Task<IActionResult> Query(GetServiceRequestsInput input)
        {
            var g = EnsurePageAccess("Requests"); if (g != null) return g;
            try
            {
                input.MaxResultCount = input.MaxResultCount > 0 ? input.MaxResultCount : 1000;
                var items = await _requestAppService.GetAllAsync(input);
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.IsManager = IsManager();
                ViewBag.Input = input;
                return View(items);
            }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/Query");
                TempData["Uyari"] = "Talep sorgulanırken bir sorun oluştu.";
                return Redirect("/Requests");
            }
        }

        public async Task<IActionResult> ExportQueryExcel(GetServiceRequestsInput input)
        {
            var g = EnsurePageAccess("Requests"); if (g != null) return g;
            input.MaxResultCount = 10000;
            var items = await _requestAppService.GetAllAsync(input);
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Talep Sorgu");
            var headers = new[] { "Talep No", "Kaynak", "Başlık", "Talep Eden", "Atanan", "Durum", "Önem", "Efor (saat)", "Geliş", "SLA" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            int row = 2;
            foreach (var r in items)
            {
                ws.Cell(row, 1).Value = r.ExternalRef ?? "";
                ws.Cell(row, 2).Value = r.SourceText;
                ws.Cell(row, 3).Value = r.Title;
                ws.Cell(row, 4).Value = r.RequesterName ?? "";
                ws.Cell(row, 5).Value = r.AssignedEmployeeName ?? "";
                ws.Cell(row, 6).Value = r.StatusText;
                ws.Cell(row, 7).Value = r.PriorityScore;
                ws.Cell(row, 8).Value = r.TotalHours;
                ws.Cell(row, 9).Value = r.ReceivedDate?.ToString("dd.MM.yyyy") ?? "";
                ws.Cell(row, 10).Value = r.DueDate?.ToString("dd.MM.yyyy") ?? "";
                row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"TalepSorgu_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // Talepler ana ekranı: Sunucu / Destek sekmeleri.
        public async Task<IActionResult> Index()
        {
            var g = EnsurePageAccess("Requests"); if (g != null) return g;
            try
            {
                // (A3) Verimli: sekme başına SINIRLI liste + gerçek SQL sayaçları (tüm talepleri belleğe yüklemez).
                // Aktif = arşiv değil + İptal değil; Arşiv = (Kapandı/Çözüldü) + efor girilmiş.
                var idx = await _requestAppService.GetIndexAsync(500);
                ViewBag.Sunucu = idx.ActiveSunucu;
                ViewBag.Destek = idx.ActiveDestek;
                ViewBag.Kapatilan = idx.Archived;
                ViewBag.CountSunucu = idx.CountSunucu;
                ViewBag.CountDestek = idx.CountDestek;
                ViewBag.CountArchived = idx.CountArchived;
                ViewBag.Cap = idx.Cap;
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                ViewBag.ActivityTypes = await _activityTypeAppService.GetAllAsync(onlyActive: true);
                ViewBag.IsManager = IsManager();
                return View();
            }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/Index");
                TempData["Uyari"] = "Talepler yüklenirken bir sorun oluştu.";
                return Redirect("/");
            }
        }

        // Dış Destek talep detayı. PSM (Sunucu Kurulum) talepleri farklı arayüze (PsmDetail) yönlendirilir.
        public async Task<IActionResult> Detail(long id)
        {
            var g = EnsurePageAccess("Requests"); if (g != null) return g;
            try
            {
                var dto = await _requestAppService.GetAsync(id);
                if (dto == null) { TempData["Uyari"] = "Talep bulunamadı."; return RedirectToAction("Index"); }
                if (dto.Source == ActivityManagement.Entities.RequestSource.SunucuKurulum)
                    return RedirectToAction("PsmDetail", new { id });
                ViewBag.Efforts = await _requestAppService.GetEffortsAsync(id);
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.ActivityTypes = await _activityTypeAppService.GetAllAsync(onlyActive: true);
                return View(dto);
            }
            catch (Abp.UI.UserFriendlyException) { TempData["Uyari"] = "Talep bulunamadı."; return RedirectToAction("Index"); }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/Detail");
                TempData["Uyari"] = "Talep açılırken bir sorun oluştu.";
                return RedirectToAction("Index");
            }
        }

        // (Y5) PSM (Sunucu Kurulum) talep detayı — sunucu künyesi odaklı AYRI arayüz. Destek talebi buraya gelirse Detail'e döner.
        public async Task<IActionResult> PsmDetail(long id)
        {
            var g = EnsurePageAccess("Requests"); if (g != null) return g;
            try
            {
                var dto = await _requestAppService.GetAsync(id);
                if (dto == null) { TempData["Uyari"] = "Talep bulunamadı."; return RedirectToAction("Index"); }
                if (dto.Source != ActivityManagement.Entities.RequestSource.SunucuKurulum)
                    return RedirectToAction("Detail", new { id });
                ViewBag.Efforts = await _requestAppService.GetEffortsAsync(id);
                ViewBag.ActivityTypes = await _activityTypeAppService.GetAllAsync(onlyActive: true);
                return View(dto);
            }
            catch (Abp.UI.UserFriendlyException) { TempData["Uyari"] = "Talep bulunamadı."; return RedirectToAction("Index"); }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/PsmDetail");
                TempData["Uyari"] = "Talep açılırken bir sorun oluştu.";
                return RedirectToAction("Index");
            }
        }

        // (C12) Portal dosya ekini token'lı olarak sunucu-içi indirip tarayıcıya akıtır (token sızmaz).
        public async Task<IActionResult> Attachment(long id, long attId)
        {
            var g = EnsurePageAccess("Requests"); if (g != null) return g;
            try
            {
                var file = await _requestAppService.DownloadPortalAttachmentAsync(id, attId);
                if (file?.Content == null || file.Content.Length == 0)
                { TempData["Uyari"] = "Dosya indirilemedi."; return RedirectToAction("Detail", new { id }); }
                return File(file.Content, file.ContentType ?? "application/octet-stream", file.FileName ?? "dosya");
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; return RedirectToAction("Detail", new { id }); }
            catch (Exception ex)
            {
                ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/Attachment");
                TempData["Uyari"] = "Dosya indirilirken bir sorun oluştu.";
                return RedirectToAction("Detail", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUpdateServiceRequestDto input)
        {
            try
            {
                await _requestAppService.CreateAsync(input);
                TempData["Success"] = "Talep oluşturuldu.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/Create"); TempData["Uyari"] = "Talep oluşturulamadı."; }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(long id, long? assignedEmployeeId, long? secondaryEmployeeId, string returnUrl = null)
        {
            try
            {
                await _requestAppService.AssignAsync(id, assignedEmployeeId, secondaryEmployeeId);
                TempData["Success"] = "Talep atandı.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/Assign"); TempData["Uyari"] = "Atama yapılamadı."; }
            return SafeBack(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(long id, RequestStatus status, int percentage, string note = null, string returnUrl = null)
        {
            try
            {
                await _requestAppService.UpdateStatusAsync(id, status, percentage, note);
                TempData["Success"] = "Durum güncellendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/UpdateStatus"); TempData["Uyari"] = "Durum güncellenemedi."; }
            return SafeBack(returnUrl);
        }

        // Destek talebinde durumu destek'in 9'lu listesiyle güncelle (kod portala POST edilir).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePortalStatus(long id, string statusCode, string note = null, string returnUrl = null)
        {
            try
            {
                await _requestAppService.UpdatePortalStatusAsync(id, statusCode, note);
                TempData["Success"] = "Durum güncellendi (portala işlendi).";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/UpdatePortalStatus"); TempData["Uyari"] = "Durum güncellenemedi."; }
            return SafeBack(returnUrl);
        }

        // (C13/V3) Portal talebine yorum + opsiyonel dosya ekle → portala (multipart) POST. isInternal=false müşteriye e-posta.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Mvc.RequestSizeLimit(60_000_000)]  // ekli dosyalar için (portal dosya başı 25 MB)
        public async Task<IActionResult> AddComment(long id, string body, bool isInternal = false,
            List<Microsoft.AspNetCore.Http.IFormFile> files = null, string returnUrl = null)
        {
            try
            {
                var uploads = new List<ActivityManagement.ServiceRequests.Dto.CommentUploadFile>();
                if (files != null)
                {
                    foreach (var f in files)
                    {
                        if (f == null || f.Length <= 0) continue;
                        using var ms = new System.IO.MemoryStream();
                        await f.CopyToAsync(ms);
                        uploads.Add(new ActivityManagement.ServiceRequests.Dto.CommentUploadFile
                        {
                            Content = ms.ToArray(),
                            FileName = System.IO.Path.GetFileName(f.FileName),
                            ContentType = f.ContentType
                        });
                    }
                }
                await _requestAppService.AddCommentAsync(id, body, isInternal, uploads);
                TempData["Success"] = isInternal ? "Dahili not eklendi." : "Yorum eklendi (müşteriye iletildi).";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/AddComment"); TempData["Uyari"] = "Yorum eklenemedi."; }
            return SafeBack(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogEffort(long serviceRequestId, decimal hoursSpent, string description, string activityType, DateTime? activityDate, string returnUrl = null)
        {
            try
            {
                await _requestAppService.LogEffortAsync(new CreateActivityLogDto
                {
                    ServiceRequestId = serviceRequestId,
                    HoursSpent = hoursSpent,
                    Description = description,
                    ActivityType = string.IsNullOrWhiteSpace(activityType) ? "Talep" : activityType,
                    ActivityDate = activityDate ?? DateTime.Today
                });
                TempData["Success"] = "Efor eklendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/LogEffort"); TempData["Uyari"] = "Efor eklenemedi."; }
            return SafeBack(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                await _requestAppService.DeleteAsync(id);
                TempData["Success"] = "Talep silindi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/Delete"); TempData["Uyari"] = "Talep silinemedi."; }
            return RedirectToAction("Index");
        }

        private IActionResult SafeBack(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index");
        }
    }
}
