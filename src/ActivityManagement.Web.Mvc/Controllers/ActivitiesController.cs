using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Activities;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Categories;
using ActivityManagement.Employees;
using ActivityManagement.Projects;
using ActivityManagement.Tasks;

namespace ActivityManagement.Web.Controllers
{
    // Faaliyet Konuları: lider/admin tanımlar, uzman efor girer. Yetki AppService'te (manuel claim) kontrol edilir.
    public class ActivitiesController : ActivityManagementControllerBase
    {
        private readonly IActivitySubjectAppService _subjectAppService;
        private readonly ICategoryAppService _categoryAppService;
        private readonly IEmployeeAppService _employeeAppService;
        private readonly IProjectAppService _projectAppService;
        private readonly IActivityTypeAppService _activityTypeAppService;
        private readonly ITaskItemAppService _taskAppService;
        private readonly ActivityManagement.ServiceRequests.IServiceRequestAppService _requestAppService;

        public ActivitiesController(
            IActivitySubjectAppService subjectAppService,
            ICategoryAppService categoryAppService,
            IEmployeeAppService employeeAppService,
            IProjectAppService projectAppService,
            IActivityTypeAppService activityTypeAppService,
            ITaskItemAppService taskAppService,
            ActivityManagement.ServiceRequests.IServiceRequestAppService requestAppService)
        {
            _subjectAppService = subjectAppService;
            _categoryAppService = categoryAppService;
            _employeeAppService = employeeAppService;
            _projectAppService = projectAppService;
            _activityTypeAppService = activityTypeAppService;
            _taskAppService = taskAppService;
            _requestAppService = requestAppService;
        }

        private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("TakımLideri");

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        // R1: Günlük Efor sayfası — seçili günün görev+faaliyetleri, toplam/eksik efor, 8 saate tamamla.
        public async Task<IActionResult> Today(DateTime? date = null)
        {
            var g = EnsurePageAccess("DailyEffort"); if (g != null) return g;
            var day = (date ?? DateTime.Today).Date;
            ViewBag.Day = day;
            ViewBag.DayEffort = await _subjectAppService.GetDayEffortsAsync(day);
            ViewBag.ActivityTypes = await _activityTypeAppService.GetAllAsync(onlyActive: true);
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;

            // O güne ait görevler (bilgi) + efor girişinde seçilebilecek aktif görevlerim (görev'e efor)
            var myId = CurrentEmployeeId();
            var dayTasks = new System.Collections.Generic.List<ActivityManagement.Tasks.Dto.TaskItemDto>();
            var myActiveTasks = new System.Collections.Generic.List<ActivityManagement.Tasks.Dto.TaskItemDto>();
            if (myId.HasValue)
            {
                var mine = (await _taskAppService.GetEmployeeTasksAsync(myId.Value)).Items;
                dayTasks = mine.Where(t =>
                    (t.StartDate.HasValue && t.StartDate.Value.Date == day) ||
                    (t.DueDate.HasValue && t.DueDate.Value.Date == day)).ToList();
                myActiveTasks = mine.Where(t => t.Status != ActivityManagement.Entities.TaskStatus.Tamamlandi
                                             && t.Status != ActivityManagement.Entities.TaskStatus.Kapatildi
                                             && t.Status != ActivityManagement.Entities.TaskStatus.Iptal).ToList();
            }
            ViewBag.DayTasks = dayTasks;
            ViewBag.MyTasks = myActiveTasks;
            // Bana atanan açık talepler (efor girişinde seçilebilsin)
            ViewBag.MyRequests = await _requestAppService.GetAllAsync(
                new ActivityManagement.ServiceRequests.Dto.GetServiceRequestsInput { MineOnly = true, OnlyOpen = true });
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDayEffort(DateTime date, decimal hoursSpent, string description, string activityType, long? taskItemId, long? projectId, long? serviceRequestId)
        {
            try
            {
                await _subjectAppService.AddManualEffortAsync(date, hoursSpent, description, activityType, taskItemId, projectId, serviceRequestId);
                TempData["Success"] = "Efor kaydı eklendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Today", new { date = date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDayEffort(long id, decimal hoursSpent, string description, DateTime activityDate, string activityType, long? projectId)
        {
            try
            {
                await _subjectAppService.UpdateEffortAsync(id, hoursSpent, description, activityDate, activityType, projectId);
                TempData["Success"] = "Efor kaydı güncellendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Today", new { date = activityDate.ToString("yyyy-MM-dd") });
        }

        public async Task<IActionResult> Index(long? projectId = null, string search = null)
        {
            var g = EnsurePageAccess("Activities"); if (g != null) return g;
            var subjects = await _subjectAppService.GetAllAsync(new GetActivitySubjectsInput { MaxResultCount = 1000, Search = search });
            ViewBag.Search = search;
            ViewBag.IsManager = IsManager();
            ViewBag.Categories = await _categoryAppService.GetAllAsync(onlyActive: true);
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
            ViewBag.ActivityTypes = await _activityTypeAppService.GetAllAsync(onlyActive: true);
            ViewBag.PreselectProjectId = projectId;   // proje detayından "Faaliyet Ekle" ile gelince modal ön-seçer
            return View(subjects);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubject(CreateUpdateActivitySubjectDto input)
        {
            try
            {
                await _subjectAppService.CreateAsync(input);
                TempData["Success"] = "Faaliyet konusu oluşturuldu.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubject(CreateUpdateActivitySubjectDto input)
        {
            try
            {
                await _subjectAppService.UpdateAsync(input);
                TempData["Success"] = "Faaliyet konusu güncellendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Detail", new { id = input.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubject(long id)
        {
            try
            {
                await _subjectAppService.DeleteAsync(id);
                TempData["Success"] = "Faaliyet konusu silindi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Detail(long id)
        {
            var subject = await _subjectAppService.GetAsync(id);
            ViewBag.Efforts = await _subjectAppService.GetEffortsAsync(id);
            ViewBag.IsManager = IsManager();
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.ActivityTypes = await _activityTypeAppService.GetAllAsync(onlyActive: true);
            return View(subject);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogEffort(CreateActivityLogDto input)
        {
            try
            {
                await _subjectAppService.LogEffortAsync(input);
                TempData["Success"] = "Efor kaydı eklendi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Detail", new { id = input.ActivitySubjectId });
        }

        // V4/R1: Eforu 8 saate tamamla (1. sorumlu sistemlerden 1'er saat rutin kontrol)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteDayTo8Hours(DateTime? date)
        {
            var day = (date ?? DateTime.Today).Date;
            try
            {
                var created = await _subjectAppService.CompleteDayTo8HoursAsync(day);
                TempData["Success"] = $"{created} adet 1'er saatlik rutin kontrol eforu eklendi (gün 8 saate tamamlandı).";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Today", new { date = day.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDayEffort(long id, DateTime date)
        {
            try
            {
                await _subjectAppService.DeleteEffortAsync(id);
                TempData["Success"] = "Efor kaydı silindi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Today", new { date = date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEffort(long id, long activitySubjectId)
        {
            try
            {
                await _subjectAppService.DeleteEffortAsync(id);
                TempData["Success"] = "Efor kaydı silindi.";
            }
            catch (Abp.UI.UserFriendlyException ex) { TempData["Uyari"] = ex.Message; }
            return RedirectToAction("Detail", new { id = activitySubjectId });
        }
    }
}
