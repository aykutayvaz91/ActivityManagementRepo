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

        // Kanban panosu
        public IActionResult Board()
        {
            return View();
        }

        public async Task<IActionResult> Detail(long id)
        {
            var task = await _taskAppService.GetAsync(id);
            return View(task);
        }

        private bool IsManager() => User.IsInRole("Admin") || User.IsInRole("TakımLideri");

        public async Task<IActionResult> Create(long? projectId, long? assignedEmployeeId, long? parentTaskId)
        {
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
            ViewBag.ParentTasks = (await _taskAppService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 })).Items;
            var dto = new CreateUpdateTaskItemDto
            {
                ProjectId = projectId,
                AssignedEmployeeId = assignedEmployeeId,
                ParentTaskId = parentTaskId
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
                ViewBag.ParentTasks = (await _taskAppService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 })).Items;
                return View(input);
            }
            try
            {
                await _taskAppService.CreateAsync(input);
                return RedirectToAction("Index");
            }
            catch (Abp.UI.UserFriendlyException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                ViewBag.ParentTasks = (await _taskAppService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 })).Items;
                return View(input);
            }
        }

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        public async Task<IActionResult> Edit(long id)
        {
            var task = await _taskAppService.GetAsync(id);
            var myEmpId = CurrentEmployeeId();
            if (!IsManager() && !(task.AssignedEmployeeId.HasValue && myEmpId.HasValue && task.AssignedEmployeeId == myEmpId))
                return Redirect("/Account/Denied");
            ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
            ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
            ViewBag.ParentTasks = (await _taskAppService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 })).Items;
            return View(ObjectMapper.Map<CreateUpdateTaskItemDto>(task));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CreateUpdateTaskItemDto input)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Employees = (await _employeeAppService.GetAllListAsync()).Items;
                ViewBag.Projects = (await _projectAppService.GetAllListAsync()).Items;
                ViewBag.ParentTasks = (await _taskAppService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 })).Items;
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
                ViewBag.ParentTasks = (await _taskAppService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000 })).Items;
                return View(input);
            }
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
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Ok(new { success = true });
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
