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
using ActivityManagement.Workflow;

namespace ActivityManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : ActivityManagementControllerBase
    {
        private readonly IEmployeeAppService _employeeAppService;
        private readonly IProjectAppService _projectAppService;
        private readonly ITaskItemAppService _taskAppService;
        private readonly IWorkflowStatusAppService _workflowStatusAppService;

        public AdminController(
            IEmployeeAppService employeeAppService,
            IProjectAppService projectAppService,
            ITaskItemAppService taskAppService,
            IWorkflowStatusAppService workflowStatusAppService)
        {
            _employeeAppService = employeeAppService;
            _projectAppService = projectAppService;
            _taskAppService = taskAppService;
            _workflowStatusAppService = workflowStatusAppService;
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

        // Durum (workflow) yönetimi
        public async Task<IActionResult> Statuses()
        {
            var list = await _workflowStatusAppService.GetAllAsync(false);
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStatus(CreateUpdateWorkflowStatusDto input)
        {
            if (input.Id > 0) await _workflowStatusAppService.UpdateAsync(input);
            else await _workflowStatusAppService.CreateAsync(input);
            TempData["Success"] = "Durum kaydedildi.";
            return RedirectToAction("Statuses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStatus(int id)
        {
            await _workflowStatusAppService.DeleteAsync(id);
            TempData["Success"] = "Durum silindi.";
            return RedirectToAction("Statuses");
        }
    }
}
