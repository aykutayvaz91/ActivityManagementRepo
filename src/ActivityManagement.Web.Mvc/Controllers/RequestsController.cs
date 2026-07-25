using System;
using System.Linq;
using System.Threading.Tasks;
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

        // Talepler ana ekranı: Sunucu / Destek sekmeleri.
        public async Task<IActionResult> Index()
        {
            var g = EnsurePageAccess("Requests"); if (g != null) return g;
            try
            {
                ViewBag.Sunucu = await _requestAppService.GetAllAsync(new GetServiceRequestsInput { Source = RequestSource.SunucuKurulum });
                ViewBag.Destek = await _requestAppService.GetAllAsync(new GetServiceRequestsInput { Source = RequestSource.DisDestek });
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

        public async Task<IActionResult> Detail(long id)
        {
            var g = EnsurePageAccess("Requests"); if (g != null) return g;
            try
            {
                var dto = await _requestAppService.GetAsync(id);
                if (dto == null) { TempData["Uyari"] = "Talep bulunamadı."; return RedirectToAction("Index"); }
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
        public async Task<IActionResult> UpdateStatus(long id, RequestStatus status, int percentage, string returnUrl = null)
        {
            try
            {
                await _requestAppService.UpdateStatusAsync(id, status, percentage);
                TempData["Success"] = "Durum güncellendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "Requests/UpdateStatus"); TempData["Uyari"] = "Durum güncellenemedi."; }
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
