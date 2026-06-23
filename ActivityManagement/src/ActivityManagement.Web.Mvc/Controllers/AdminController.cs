using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Employees;
using ActivityManagement.Employees.Dto;
using ActivityManagement.Projects;
using ActivityManagement.Projects.Dto;
using ActivityManagement.Tasks;
using ActivityManagement.Tasks.Dto;

namespace ActivityManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : ActivityManagementControllerBase
    {
        private readonly IEmployeeAppService _employeeAppService;
        private readonly IProjectAppService _projectAppService;
        private readonly ITaskItemAppService _taskAppService;

        public AdminController(
            IEmployeeAppService employeeAppService,
            IProjectAppService projectAppService,
            ITaskItemAppService taskAppService)
        {
            _employeeAppService = employeeAppService;
            _projectAppService = projectAppService;
            _taskAppService = taskAppService;
        }

        public async Task<IActionResult> Index()
        {
            var emps = await _employeeAppService.GetAllAsync(new GetEmployeesInput { MaxResultCount = 1000 });
            var prjs = await _projectAppService.GetAllAsync(new GetProjectsInput { MaxResultCount = 1000 });
            var tasks = await _taskAppService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 });

            ViewBag.EmployeeCount = emps.TotalCount;
            ViewBag.ProjectCount = prjs.TotalCount;
            ViewBag.TaskCount = tasks.TotalCount;
            ViewBag.AdminCount = emps.Items.Count(e => e.AppRole == "Admin");
            return View();
        }

        // Rol yönetimi
        public async Task<IActionResult> Roles()
        {
            var emps = await _employeeAppService.GetAllAsync(new GetEmployeesInput { MaxResultCount = 1000 });
            return View(emps.Items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRole(long id, string appRole)
        {
            await _employeeAppService.UpdateRoleAsync(id, appRole);
            TempData["Success"] = "Rol güncellendi.";
            return RedirectToAction("Roles");
        }
    }
}
