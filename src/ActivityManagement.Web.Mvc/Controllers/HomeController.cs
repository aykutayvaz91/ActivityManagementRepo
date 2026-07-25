using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
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

        private long? CurrentEmployeeId()
        {
            var c = User.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        public async Task<IActionResult> Index()
        {
            // Rol bazlı görünürlük: Admin tüm veriyi; TakımLideri/Uzman yalnızca kendi takımını görür
            bool isAdmin = User.IsInRole("Admin");
            long? myTeam = null;
            if (!isAdmin)
            {
                var eid = CurrentEmployeeId();
                myTeam = eid.HasValue ? (await _employeeService.GetAsync(eid.Value)).TeamId : null;
            }

            var employees = (await _employeeService.GetAllAsync(new Employees.Dto.GetEmployeesInput { MaxResultCount = 1000 })).Items;
            var projects = (await _projectService.GetAllAsync(new Projects.Dto.GetProjectsInput { MaxResultCount = 1000 })).Items;
            var tasks = (await _taskService.GetAllAsync(new GetTasksInput { MaxResultCount = 1000, TeamId = isAdmin ? null : myTeam })).Items;

            if (!isAdmin)
            {
                employees = employees.Where(e => e.TeamId == myTeam).ToList();
                // Takımsız (henüz atanmamış) projeler de görünsün; aksi halde non-admin hiç proje göremez
                projects = projects.Where(p => p.TeamId == myTeam || p.TeamId == null).ToList();
            }

            // Kapatıldı (arşiv) görevler aktif panoda/sayaçlarda gösterilmez.
            var activeTasks = tasks.Where(t => t.Status != Entities.TaskStatus.Kapatildi).ToList();
            ViewBag.EmployeeCount = employees.Count;
            ViewBag.ProjectCount = projects.Count;
            ViewBag.TotalTaskCount = activeTasks.Count;
            ViewBag.PendingTaskCount = activeTasks.Count(t => t.Status == Entities.TaskStatus.Beklemede);
            ViewBag.InProgressTaskCount = activeTasks.Count(t => t.Status == Entities.TaskStatus.DevamEdiyor);
            ViewBag.CompletedTaskCount = activeTasks.Count(t => t.Status == Entities.TaskStatus.Tamamlandi);
            // "Aktif Projeler": tamamlanmamış/iptal edilmemiş (Planlandı + Devam) tüm açık projeler
            ViewBag.ActiveProjects = projects
                .Where(p => p.Status != Entities.ProjectStatus.Tamamlandi && p.Status != Entities.ProjectStatus.Iptal)
                .OrderByDescending(p => p.Status)
                .Take(6).ToList();
            ViewBag.RecentTasks = activeTasks.OrderByDescending(t => t.CreationTime).Take(8).ToList();

            return View();
        }

        // Global hata sayfası (UseExceptionHandler buraya yönlendirir). Hatayı dosyaya loglar, kullanıcıya dostça gösterir.
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Error()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (feature?.Error != null)
            {
                try { ActivityManagement.Logging.ErrorLog.Write(feature.Error, $"Path={feature.Path}"); } catch { }
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return StatusCode(500, new { error = "Beklenmeyen bir hata oluştu." });

            Response.StatusCode = 500;
            ViewBag.Path = feature?.Path;
            return View();
        }
    }
}
