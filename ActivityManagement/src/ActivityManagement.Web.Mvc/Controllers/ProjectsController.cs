using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Authorization;
using ActivityManagement.Employees;
using ActivityManagement.Projects;
using ActivityManagement.Projects.Dto;

namespace ActivityManagement.Web.Controllers
{
    public class ProjectsController : ActivityManagementControllerBase
    {
        private readonly IProjectAppService _projectAppService;
        private readonly IEmployeeAppService _employeeAppService;

        public ProjectsController(IProjectAppService projectAppService, IEmployeeAppService employeeAppService)
        {
            _projectAppService = projectAppService;
            _employeeAppService = employeeAppService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Detail(long id)
        {
            var project = await _projectAppService.GetAsync(id);
            return View(project);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            return View(new CreateUpdateProjectDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUpdateProjectDto input)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                return View(input);
            }
            await _projectAppService.CreateAsync(input);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(long id)
        {
            var project = await _projectAppService.GetAsync(id);
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            return View(ObjectMapper.Map<CreateUpdateProjectDto>(project));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreateUpdateProjectDto input)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                return View(input);
            }
            await _projectAppService.UpdateAsync(input);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(long id)
        {
            await _projectAppService.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddMember(long projectId, long employeeId, string role, bool isManager)
        {
            await _projectAppService.AddMemberAsync(projectId, employeeId, role, isManager);
            return RedirectToAction("Detail", new { id = projectId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveMember(long projectId, long employeeId)
        {
            await _projectAppService.RemoveMemberAsync(projectId, employeeId);
            return RedirectToAction("Detail", new { id = projectId });
        }
    }
}
