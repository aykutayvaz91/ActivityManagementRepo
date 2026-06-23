using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Employees;
using ActivityManagement.Projects;
using ActivityManagement.Tasks;
using ActivityManagement.Tasks.Dto;

namespace ActivityManagement.Web.Controllers
{
    public class HomeController : ActivityManagementControllerBase
    {
        private readonly IEmployeeAppService _employeeService;
        private readonly IProjectAppService _projectService;
        private readonly ITaskItemAppService _taskService;

        public HomeController(
            IEmployeeAppService employeeService,
            IProjectAppService projectService,
            ITaskItemAppService taskService)
        {
            _employeeService = employeeService;
            _projectService = projectService;
            _taskService = taskService;
        }

        public async Task<IActionResult> Index()
        {
            var employees = await _employeeService.GetAllAsync(new Employees.Dto.GetEmployeesInput { MaxResultCount = 1000 });
            var projects = await _projectService.GetAllAsync(new Projects.Dto.GetProjectsInput { MaxResultCount = 1000 });
            var tasks = await _taskService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 });

            ViewBag.EmployeeCount = employees.TotalCount;
            ViewBag.ProjectCount = projects.TotalCount;
            ViewBag.TotalTaskCount = tasks.TotalCount;
            ViewBag.PendingTaskCount = tasks.Items.Count(t => t.Status == Entities.TaskStatus.Beklemede);
            ViewBag.InProgressTaskCount = tasks.Items.Count(t => t.Status == Entities.TaskStatus.DevamEdiyor);
            ViewBag.CompletedTaskCount = tasks.Items.Count(t => t.Status == Entities.TaskStatus.Tamamlandi);
            ViewBag.ActiveProjects = projects.Items.Where(p => p.Status == Entities.ProjectStatus.Devam).Take(5).ToList();
            ViewBag.RecentTasks = tasks.Items.OrderByDescending(t => t.CreationTime).Take(8).ToList();

            return View();
        }
    }
}
