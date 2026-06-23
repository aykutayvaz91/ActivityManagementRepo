using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActivityManagement.Authorization;
using ActivityManagement.Employees;
using ActivityManagement.Projects;
using ActivityManagement.Tasks;
using ActivityManagement.Tasks.Dto;

namespace ActivityManagement.Web.Controllers
{
    public class TasksController : ActivityManagementControllerBase
    {
        private readonly ITaskItemAppService _taskAppService;
        private readonly IEmployeeAppService _employeeAppService;
        private readonly IProjectAppService _projectAppService;

        public TasksController(
            ITaskItemAppService taskAppService,
            IEmployeeAppService employeeAppService,
            IProjectAppService projectAppService)
        {
            _taskAppService = taskAppService;
            _employeeAppService = employeeAppService;
            _projectAppService = projectAppService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Detail(long id)
        {
            var task = await _taskAppService.GetAsync(id);
            ViewBag.CanModify = (User.IsInRole("Admin") || User.IsInRole("TakımLideri"))
                                || task.AssignedEmployeeId == CurrentEmployeeId();
            return View(task);
        }

        private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("TakımLideri");

        public async Task<IActionResult> Create(long? projectId, long? assignedEmployeeId)
        {
            if (!IsManager()) return Redirect("/Account/Denied");
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
            var dto = new CreateUpdateTaskItemDto
            {
                ProjectId = projectId,
                AssignedEmployeeId = assignedEmployeeId
            };
            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUpdateTaskItemDto input)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                return View(input);
            }
            await _taskAppService.CreateAsync(input);
            return RedirectToAction("Index");
        }

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        public async Task<IActionResult> Edit(long id)
        {
            var task = await _taskAppService.GetAsync(id);
            // Uzman yalnızca kendi görevini düzenleyebilir
            if (!IsManager() && task.AssignedEmployeeId != CurrentEmployeeId())
                return Redirect("/Account/Denied");
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
            return View(ObjectMapper.Map<CreateUpdateTaskItemDto>(task));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreateUpdateTaskItemDto input)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                return View(input);
            }
            await _taskAppService.UpdateAsync(input);
            return RedirectToAction("Index");
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
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(long taskId, string comment)
        {
            await _taskAppService.AddCommentAsync(taskId, comment);
            return RedirectToAction("Detail", new { id = taskId });
        }
    }
}
