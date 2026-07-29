using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;
using ActivityManagement.Search.Dto;

namespace ActivityManagement.Search
{
    // Üst bar global araması. Görünürlük KONSERVATİF uygulanır (başlık sızıntısı olmasın); tıklanınca
    // detay sayfası kendi yetki kontrolünü ayrıca yapar. Admin/Manager tümü; TakımLideri takımı; Uzman kendine ait.
    public class GlobalSearchAppService : ActivityManagementAppServiceBase, IGlobalSearchAppService
    {
        private readonly IRepository<TaskItem, long> _taskRepo;
        private readonly IRepository<ServiceRequest, long> _requestRepo;
        private readonly IRepository<ActivitySubject, long> _subjectRepo;
        private readonly IRepository<Project, long> _projectRepo;
        private readonly IRepository<Employee, long> _employeeRepo;
        private readonly IHttpContextAccessor _http;

        public GlobalSearchAppService(
            IRepository<TaskItem, long> taskRepo,
            IRepository<ServiceRequest, long> requestRepo,
            IRepository<ActivitySubject, long> subjectRepo,
            IRepository<Project, long> projectRepo,
            IRepository<Employee, long> employeeRepo,
            IHttpContextAccessor http)
        {
            _taskRepo = taskRepo;
            _requestRepo = requestRepo;
            _subjectRepo = subjectRepo;
            _projectRepo = projectRepo;
            _employeeRepo = employeeRepo;
            _http = http;
        }

        public async Task<GlobalSearchResultDto> SearchAsync(string q, int perType = 8)
        {
            var result = new GlobalSearchResultDto { Query = q };
            q = q?.Trim();
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return result;   // en az 2 karakter
            if (perType < 1) perType = 1; else if (perType > 25) perType = 25;

            var user = _http.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
            bool seesAll = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
            bool isLeader = string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);
            long? myTeam = empId.HasValue
                ? await _employeeRepo.GetAll().AsNoTracking().Where(x => x.Id == empId.Value).Select(x => x.TeamId).FirstOrDefaultAsync()
                : null;

            // GÖREVLER
            var tq = _taskRepo.GetAll().AsNoTracking().Where(t => t.Title.Contains(q));
            if (!seesAll)
                tq = tq.Where(t => (empId != null && t.AssignedEmployeeId == empId) || (myTeam != null && t.TeamId == myTeam));
            result.Tasks = (await tq.OrderByDescending(t => t.CreationTime).Take(perType)
                .Select(t => new { t.Id, t.Title, t.Status }).ToListAsync())
                .Select(t => new SearchHitDto { Type = "Görev", Icon = "fa-clipboard-list", Id = t.Id, Title = t.Title, Subtitle = t.Status.ToString(), Url = "/Tasks/Detail/" + t.Id })
                .ToList();

            // TALEPLER (PSM → PsmDetail, destek → Detail)
            var rq = _requestRepo.GetAll().AsNoTracking()
                .Where(r => r.Title.Contains(q) || (r.ExternalRef != null && r.ExternalRef.Contains(q)) || (r.RequesterName != null && r.RequesterName.Contains(q)));
            if (!seesAll)
                rq = rq.Where(r => (empId != null && (r.AssignedEmployeeId == empId || r.SecondaryEmployeeId == empId)) || (myTeam != null && r.TeamId == myTeam));
            result.Requests = (await rq.OrderByDescending(r => r.CreationTime).Take(perType)
                .Select(r => new { r.Id, r.Title, r.ExternalRef, r.Source, r.PortalStatusText }).ToListAsync())
                .Select(r => new SearchHitDto
                {
                    Type = "Talep", Icon = "fa-inbox", Id = r.Id, Title = r.Title,
                    Subtitle = (r.ExternalRef ?? "") + (string.IsNullOrEmpty(r.PortalStatusText) ? "" : " · " + r.PortalStatusText),
                    Url = (r.Source == RequestSource.SunucuKurulum ? "/Requests/PsmDetail/" : "/Requests/Detail/") + r.Id
                }).ToList();

            // FAALİYETLER (Admin/Manager/TakımLideri tümü; Uzman kendi)
            var aq = _subjectRepo.GetAll().AsNoTracking().Where(s => s.Title.Contains(q));
            if (!(seesAll || isLeader))
                aq = aq.Where(s => (empId != null && s.AssignedEmployeeId == empId) || (empId != null && s.CreatedByLeaderId == empId));
            result.Activities = (await aq.OrderByDescending(s => s.CreationTime).Take(perType)
                .Select(s => new { s.Id, s.Title }).ToListAsync())
                .Select(s => new SearchHitDto { Type = "Faaliyet", Icon = "fa-clipboard-check", Id = s.Id, Title = s.Title, Url = "/Activities/Detail/" + s.Id })
                .ToList();

            // PROJELER
            var pq = _projectRepo.GetAll().AsNoTracking().Where(p => p.Name.Contains(q) || (p.Code != null && p.Code.Contains(q)));
            if (!seesAll)
                pq = pq.Where(p => myTeam != null && p.TeamId == myTeam);
            result.Projects = (await pq.OrderByDescending(p => p.CreationTime).Take(perType)
                .Select(p => new { p.Id, p.Name, p.Code }).ToListAsync())
                .Select(p => new SearchHitDto { Type = "Proje", Icon = "fa-diagram-project", Id = p.Id, Title = p.Name, Subtitle = p.Code, Url = "/Projects/Detail/" + p.Id })
                .ToList();

            // KİŞİLER (yalnız yöneticiler; TakımLideri kendi takımı) — FullName hesaplanmış → FirstName/LastName/Email ile ara
            if (seesAll || isLeader)
            {
                var eq = _employeeRepo.GetAll().AsNoTracking().Where(x => x.IsActive
                    && (x.FirstName.Contains(q) || x.LastName.Contains(q) || (x.Email != null && x.Email.Contains(q))));
                if (!seesAll) eq = eq.Where(x => myTeam != null && x.TeamId == myTeam);
                result.Employees = (await eq.OrderBy(x => x.FirstName).Take(perType)
                    .Select(x => new { x.Id, x.FirstName, x.LastName, x.Title }).ToListAsync())
                    .Select(x => new SearchHitDto { Type = "Kişi", Icon = "fa-user", Id = x.Id, Title = (x.FirstName + " " + x.LastName).Trim(), Subtitle = x.Title, Url = "/Employees/Card/" + x.Id })
                    .ToList();
            }

            return result;
        }
    }
}
