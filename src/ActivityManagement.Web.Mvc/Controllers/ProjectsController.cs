using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Authorization;
using ActivityManagement.Activities;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Categories;
using ActivityManagement.Employees;
using ActivityManagement.Projects;
using ActivityManagement.Projects.Dto;

namespace ActivityManagement.Web.Controllers
{
    public class ProjectsController : ActivityManagementControllerBase
    {
        private readonly IProjectAppService _projectAppService;
        private readonly IEmployeeAppService _employeeAppService;
        private readonly ICategoryAppService _categoryAppService;
        private readonly IActivitySubjectAppService _subjectAppService;

        public ProjectsController(IProjectAppService projectAppService, IEmployeeAppService employeeAppService, ICategoryAppService categoryAppService, IActivitySubjectAppService subjectAppService)
        {
            _projectAppService = projectAppService;
            _employeeAppService = employeeAppService;
            _categoryAppService = categoryAppService;
            _subjectAppService = subjectAppService;
        }

        private async Task LoadFormViewBagsAsync()
        {
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Categories = await _categoryAppService.GetAllAsync(onlyActive: true);
        }

        private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("TakımLideri");

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        public IActionResult Index()
        {
            var g = EnsurePageAccess("Projects"); if (g != null) return g;
            return View();
        }

        public async Task<IActionResult> Detail(long id)
        {
            var project = await _projectAppService.GetAsync(id);
            try
            {
                // Proje detayında projenin TÜM faaliyetleri gösterilir (kişisel/takım kapsamına takılmadan).
                ViewBag.Activities = await _subjectAppService.GetByProjectAsync(id);
            }
            catch
            {
                ViewBag.Activities = new System.Collections.Generic.List<ActivitySubjectDto>();
            }
            return View(project);
        }

        // Proje oluşturma herkese açık; yönetici olmayanlar sadece kendilerini yönetici
        // olarak atayarak (başkasını seçemeden) proje açabilir.
        public async Task<IActionResult> Create()
        {
            if (!IsManager()) return AccessDeniedRedirect(); // V4: Uzman proje oluşturamaz
            await LoadFormViewBagsAsync();
            var dto = new CreateUpdateProjectDto();
            dto.Code = await _projectAppService.GetNextCodeAsync(); // sıradaki PRJ-### otomatik gelsin
            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUpdateProjectDto input)
        {
            if (!IsManager()) return AccessDeniedRedirect(); // V4: Uzman proje oluşturamaz
            if (!ModelState.IsValid)
            {
                await LoadFormViewBagsAsync();
                return View(input);
            }
            try
            {
                await _projectAppService.CreateAsync(input);
                return RedirectToAction("Index");
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadFormViewBagsAsync();
                return View(input);
            }
        }

        // Düzenleme/silme: sadece Admin ve Takım Lideri
        public async Task<IActionResult> Edit(long id)
        {
            if (!IsManager()) return AccessDeniedRedirect();
            var project = await _projectAppService.GetAsync(id);
            await LoadFormViewBagsAsync();
            return View(ObjectMapper.Map<CreateUpdateProjectDto>(project));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreateUpdateProjectDto input)
        {
            if (!IsManager()) return AccessDeniedRedirect();
            if (!ModelState.IsValid)
            {
                await LoadFormViewBagsAsync();
                return View(input);
            }
            try
            {
                await _projectAppService.UpdateAsync(input);
                return RedirectToAction("Index");
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                ModelState.AddModelError("", ex.Message);
                await LoadFormViewBagsAsync();
                return View(input);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(long id)
        {
            if (!IsManager()) return AccessDeniedRedirect();
            try
            {
                await _projectAppService.DeleteAsync(id);
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddMember(long projectId, long employeeId, string role, bool isManager, int responsibilityLevel = 0)
        {
            try
            {
                await _projectAppService.AddMemberAsync(projectId, employeeId, role, isManager, responsibilityLevel);
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Detail", new { id = projectId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveMember(long projectId, long employeeId)
        {
            try
            {
                await _projectAppService.RemoveMemberAsync(projectId, employeeId);
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Detail", new { id = projectId });
        }
    }
}
