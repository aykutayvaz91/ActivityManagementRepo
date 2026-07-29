using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Authorization;
using ActivityManagement.Entities;
using ActivityManagement.Projects.Dto;

namespace ActivityManagement.Projects
{
    // Yetki manuel claim ile kontrol edilir (projedeki standart). Görüntüleme herkese açık;
    // güncelle/sil EnsureManager(project) ile Admin (tümü) / TakımLideri (kendi takımı) sınırlıdır.
    public class ProjectAppService : ActivityManagementAppServiceBase, IProjectAppService
    {
        private readonly IRepository<Project, long> _projectRepository;
        private readonly IRepository<ProjectEmployee, long> _projectEmployeeRepository;
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IRepository<Team, long> _teamRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProjectAppService(
            IRepository<Project, long> projectRepository,
            IRepository<ProjectEmployee, long> projectEmployeeRepository,
            IRepository<Employee, long> employeeRepository,
            IRepository<Team, long> teamRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _projectRepository = projectRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _employeeRepository = employeeRepository;
            _teamRepository = teamRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        // Proje düzenleme/silme yetkisi: Admin (her proje), TakımLideri (sadece kendi takımının projeleri).
        // Oluşturma herkese açık ama yönetici olmayanlar sadece kendilerini
        // proje yöneticisi yaparak (başkasını seçemeden) proje açabilir.
        private string CurrentRole() =>
            _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";

        private bool IsAdmin() => string.Equals(CurrentRole(), "Admin", StringComparison.OrdinalIgnoreCase);

        private bool IsManager() =>
            IsAdmin() || string.Equals(CurrentRole(), "TakımLideri", StringComparison.OrdinalIgnoreCase);

        private long? CurrentEmployeeId()
        {
            var c = _httpContextAccessor.HttpContext?.User?.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        private long? CurrentEmployeeTeamId()
        {
            var empId = CurrentEmployeeId();
            if (!empId.HasValue) return null;
            return _employeeRepository.GetAll().Where(e => e.Id == empId.Value).Select(e => e.TeamId).FirstOrDefault();
        }

        // Takım izolasyonu: Admin-self (Sistem Yöneticisi) TÜM projeleri görür; non-admin VEYA login-as başka kişi
        // → yalnız o kişinin TAKIMININ projeleri (başka takım görünmez).
        private async Task<(bool scope, long? teamId)> TeamScopeAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
            long? ownId = long.TryParse(user?.FindFirst("AdminOwnEmployeeId")?.Value, out var o) ? o : (long?)null;
            // Manager: tüm takımları görür.
            if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase)) return (false, null);
            bool isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            // Admin-self: config-admin kendi kimliğinde VEYA AdminOwnEmployeeId claim'i olmayan (Google) admin → tümünü görür.
            bool adminSelf = isAdmin && (!empId.HasValue || !ownId.HasValue || empId == ownId);
            if (adminSelf || !empId.HasValue) return (false, null);
            // Login-as ile temsil edilen kişi Manager/Admin ise TÜM projeleri görür (rol claim'i Admin kalsa bile).
            var emp = await _employeeRepository.GetAll()
                .Where(x => x.Id == empId.Value).Select(x => new { x.TeamId, x.AppRole }).FirstOrDefaultAsync();
            if (emp != null && (string.Equals(emp.AppRole, "Manager", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(emp.AppRole, "Admin", StringComparison.OrdinalIgnoreCase)))
                return (false, null);
            return (true, emp?.TeamId);
        }

        // Admin her projeyi düzenleyebilir/silebilir; TakımLideri sadece kendi takımının projesini.
        private void EnsureManager(Project project = null)
        {
            if (IsAdmin()) return;
            if (IsManager() && project != null && (!project.TeamId.HasValue || project.TeamId == CurrentEmployeeTeamId())) return;
            throw new UserFriendlyException("Bu işlem için yetkiniz yok. Projeleri Admin (tümü) veya Takım Lideri (kendi takımı) düzenleyebilir/silebilir.");
        }

        // Üye ekleme/çıkarma: Admin, proje yöneticisi veya projenin takım lideri
        private async Task EnsureCanManageMembersAsync(long projectId)
        {
            if (IsAdmin()) return;
            var myEmpId = CurrentEmployeeId();
            var project = await _projectRepository.GetAsync(projectId);

            if (myEmpId.HasValue && project.ManagerId == myEmpId.Value) return;

            if (project.TeamId.HasValue && myEmpId.HasValue)
            {
                var teamLeaderId = await _teamRepository.GetAll()
                    .Where(t => t.Id == project.TeamId.Value)
                    .Select(t => t.LeaderId)
                    .FirstOrDefaultAsync();
                if (teamLeaderId == myEmpId.Value) return;
            }

            throw new UserFriendlyException("Proje üyesi ekleme/çıkarma yetkiniz yok. Sadece Admin, proje yöneticisi veya takım lideri yapabilir.");
        }

        public async Task<PagedResultDto<ProjectDto>> GetAllAsync(GetProjectsInput input)
        {
            // AsNoTracking (salt-okuma) + AsSplitQuery: iki koleksiyon Include (ProjectEmployees+Tasks) paging ile
            // kartezyen şişme yapıyordu → ayrı sorgulara bölünür (Relational paketi eklendi).
            var query = _projectRepository.GetAll().AsNoTracking().AsSplitQuery()
                .Include(p => p.Manager)
                .Include(p => p.PrimaryResponsible)
                .Include(p => p.SecondaryResponsible)
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Team)
                .Include(p => p.ProjectEmployees)
                .Include(p => p.Tasks)
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter),
                    p => p.Name.Contains(input.Filter) || p.Code.Contains(input.Filter))
                .WhereIf(input.Status.HasValue, p => p.Status == input.Status.Value)
                .WhereIf(input.ManagerId.HasValue, p => p.ManagerId == input.ManagerId.Value);

            var (scope, teamId) = await TeamScopeAsync();
            if (scope) query = query.Where(p => p.TeamId == teamId); // yalnız kendi takımının projeleri

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(p => p.CreationTime).PageBy(input).ToListAsync();

            var dtos = items.Select(p => MapToProjectDto(p)).ToList();
            return new PagedResultDto<ProjectDto>(count, dtos);
        }

        public async Task<ProjectDto> GetAsync(long id)
        {
            var project = await _projectRepository.GetAll().AsNoTracking().AsSplitQuery()
                .Include(p => p.Manager)
                .Include(p => p.PrimaryResponsible)
                .Include(p => p.SecondaryResponsible)
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Team)
                .Include(p => p.ProjectEmployees).ThenInclude(pe => pe.Employee)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            return MapToProjectDto(project);
        }

        // Herkes proje oluşturabilir; yönetici olmayanlar sadece kendilerini
        // proje yöneticisi olarak atayabilir (başkasını seçemez), proje kendi takımına dahil edilir.
        public async Task<ProjectDto> CreateAsync(CreateUpdateProjectDto input)
        {
            // V4: Proje oluşturma yalnızca Admin/Takım Lideri; Uzman oluşturamaz.
            if (!IsManager())
                throw new UserFriendlyException("Proje oluşturma yetkiniz yok. Yalnızca Admin ve Takım Lideri proje oluşturabilir.");
            // ManagerId'yi 1. Sorumlu ile senkron tut (mevcut yetki/erişim mantığı ManagerId üzerinden çalışıyor)
            input.ManagerId = input.PrimaryResponsibleId ?? input.ManagerId;

            // Kod girilmemişse otomatik sıradaki PRJ-### kodu ata
            if (string.IsNullOrWhiteSpace(input.Code))
                input.Code = await GetNextCodeAsync();

            // Takım girilmemişse 1. Sorumlu'nun (yoksa oluşturanın) takımından miras al → takımsız proje kalmasın
            if (!input.TeamId.HasValue)
            {
                var refEmp = input.PrimaryResponsibleId ?? CurrentEmployeeId();
                if (refEmp.HasValue)
                    input.TeamId = await _employeeRepository.GetAll().AsNoTracking()
                        .Where(e => e.Id == refEmp.Value).Select(e => e.TeamId).FirstOrDefaultAsync();
            }

            var project = ObjectMapper.Map<Project>(input);
            project.TenantId = AbpSession.TenantId ?? 1;
            await _projectRepository.InsertAsync(project);
            await CurrentUnitOfWork.SaveChangesAsync();
            await SyncResponsibleMembersAsync(project); // 1./2. sorumlu aynı zamanda proje üyesi olur
            return ObjectMapper.Map<ProjectDto>(project);
        }

        // 1. ve 2. Sorumlu, proje takımına üye olarak eklenir (üye sayısı 0 görünmesin).
        // Rol etiketleri "1. Sorumlu"/"2. Sorumlu" olarak güncellenir; sorumlu değişirse eski
        // etiketli üye normal üyeye çevrilir (üyelikten çıkarılmaz).
        private async Task SyncResponsibleMembersAsync(Project project)
        {
            // Artık sorumlu olmayan eski "1./2. Sorumlu" etiketli üyeleri sade üyeye çevir
            var tagged = await _projectEmployeeRepository.GetAll()
                .Where(pe => pe.ProjectId == project.Id && (pe.Role == "1. Sorumlu" || pe.Role == "2. Sorumlu"))
                .ToListAsync();
            foreach (var pe in tagged)
            {
                if (pe.EmployeeId != project.PrimaryResponsibleId && pe.EmployeeId != project.SecondaryResponsibleId)
                {
                    pe.Role = "Üye";
                    pe.IsManager = false;
                }
            }

            async Task Upsert(long? empId, string role, bool isManager)
            {
                if (!empId.HasValue) return;
                var pe = await _projectEmployeeRepository.FirstOrDefaultAsync(
                    x => x.ProjectId == project.Id && x.EmployeeId == empId.Value);
                if (pe == null)
                    await _projectEmployeeRepository.InsertAsync(new ProjectEmployee
                    {
                        ProjectId = project.Id,
                        EmployeeId = empId.Value,
                        Role = role,
                        IsManager = isManager
                    });
                else { pe.Role = role; pe.IsManager = isManager || pe.IsManager; }
            }

            await Upsert(project.PrimaryResponsibleId, "1. Sorumlu", true);
            if (project.SecondaryResponsibleId != project.PrimaryResponsibleId)
                await Upsert(project.SecondaryResponsibleId, "2. Sorumlu", false);

            await CurrentUnitOfWork.SaveChangesAsync();
        }

        public async Task<ProjectDto> UpdateAsync(CreateUpdateProjectDto input)
        {
            var project = await _projectRepository.GetAsync(input.Id);
            EnsureManager(project);
            input.ManagerId = input.PrimaryResponsibleId ?? input.ManagerId; // senkron
            ObjectMapper.Map(input, project);
            await SyncResponsibleMembersAsync(project); // 1./2. sorumlu üyeliğini güncelle
            return ObjectMapper.Map<ProjectDto>(project);
        }

        public async Task DeleteAsync(long id)
        {
            var project = await _projectRepository.GetAsync(id);
            EnsureManager(project);
            await _projectRepository.DeleteAsync(id);
        }

        public async Task AddMemberAsync(long projectId, long employeeId, string role, bool isManager, int responsibilityLevel = 0)
        {
            await EnsureCanManageMembersAsync(projectId);

            // 1./2. Sorumlu seçildiyse rol/etiket otomatik atanır ve proje sorumlusu güncellenir
            if (responsibilityLevel == 1)
            {
                role = "1. Sorumlu";
                isManager = true;
                var project = await _projectRepository.GetAsync(projectId);
                project.PrimaryResponsibleId = employeeId;
                project.ManagerId = employeeId; // yetki mantığı ManagerId üzerinden çalışıyor
            }
            else if (responsibilityLevel == 2)
            {
                role = "2. Sorumlu";
                var project = await _projectRepository.GetAsync(projectId);
                project.SecondaryResponsibleId = employeeId;
            }

            var existing = await _projectEmployeeRepository.FirstOrDefaultAsync(
                pe => pe.ProjectId == projectId && pe.EmployeeId == employeeId);
            if (existing == null)
            {
                await _projectEmployeeRepository.InsertAsync(new ProjectEmployee
                {
                    ProjectId = projectId,
                    EmployeeId = employeeId,
                    Role = role,
                    IsManager = isManager
                });
            }
            else
            {
                existing.Role = role;
                existing.IsManager = isManager || existing.IsManager;
            }
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(long projectId, long employeeId)
        {
            await EnsureCanManageMembersAsync(projectId);

            var pe = await _projectEmployeeRepository.FirstOrDefaultAsync(
                x => x.ProjectId == projectId && x.EmployeeId == employeeId);
            if (pe != null)
                await _projectEmployeeRepository.DeleteAsync(pe);
        }

        // Sıradaki proje kodu: "PRJ-" ile başlayan mevcut kodlardaki en büyük numaradan +1 (3 hane).
        public async Task<string> GetNextCodeAsync()
        {
            const string prefix = "PRJ-";
            var codes = await _projectRepository.GetAll()
                .Where(p => p.Code != null && p.Code.StartsWith(prefix))
                .Select(p => p.Code)
                .ToListAsync();

            int max = 0;
            foreach (var c in codes)
            {
                var numPart = c.Substring(prefix.Length);
                if (int.TryParse(numPart, out var n) && n > max) max = n;
            }
            return $"{prefix}{max + 1:D3}";
        }

        public async Task<ListResultDto<ProjectDto>> GetAllListAsync()
        {
            System.Linq.IQueryable<Project> q = _projectRepository.GetAll()
                .Include(p => p.Manager)
                .Include(p => p.Team);
            var (scope, teamId) = await TeamScopeAsync();
            if (scope) q = q.Where(p => p.TeamId == teamId); // dropdown'larda da yalnız kendi takımı
            var projects = await q.OrderBy(p => p.Name).ToListAsync();
            return new ListResultDto<ProjectDto>(projects.Select(MapToProjectDto).ToList());
        }

        private ProjectDto MapToProjectDto(Project p)
        {
            var dto = ObjectMapper.Map<ProjectDto>(p);
            dto.StatusText = p.Status.ToString();
            dto.ManagerName = p.Manager?.FullName;
            dto.PrimaryResponsibleName = p.PrimaryResponsible?.FullName;
            dto.SecondaryResponsibleName = p.SecondaryResponsible?.FullName;
            dto.CategoryName = p.Category?.Name;
            dto.SubCategoryName = p.SubCategory?.Name;
            dto.TeamName = p.Team?.Name;
            dto.MemberCount = p.ProjectEmployees?.Count ?? 0;
            // Kapatıldı (arşiv) görevler aktif ilerleme %'sinde hesaba KATILMAZ (paydadan da düşülür).
            dto.TaskCount = p.Tasks?.Count(t => t.Status != Entities.TaskStatus.Kapatildi) ?? 0;
            dto.CompletedTaskCount = p.Tasks?.Count(t => t.Status == Entities.TaskStatus.Tamamlandi) ?? 0;
            dto.Members = p.ProjectEmployees?.Select(pe => new ProjectMemberDto
            {
                EmployeeId = pe.EmployeeId,
                FullName = pe.Employee?.FullName,
                Title = pe.Employee?.Title,
                Role = pe.Role,
                IsManager = pe.IsManager
            }).ToList() ?? new List<ProjectMemberDto>();
            return dto;
        }
    }
}
