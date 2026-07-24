using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Authorization;
using ActivityManagement.Employees.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Employees
{
    // Yetki manuel claim ile kontrol edilir (projedeki standart). Görüntüleme herkese açık;
    // ekle/güncelle/sil EnsureManager()/kendi-kaydı kontrolleriyle sınırlıdır.
    public class EmployeeAppService : ActivityManagementAppServiceBase, IEmployeeAppService
    {
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IRepository<Responsibility, long> _responsibilityRepository;
        private readonly IRepository<ProjectEmployee, long> _projectEmployeeRepository;
        private readonly IRepository<TaskItem, long> _taskRepository;
        private readonly IRepository<Project, long> _projectRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmployeeAppService(
            IRepository<Employee, long> employeeRepository,
            IRepository<Responsibility, long> responsibilityRepository,
            IRepository<ProjectEmployee, long> projectEmployeeRepository,
            IRepository<TaskItem, long> taskRepository,
            IRepository<Project, long> projectRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _employeeRepository = employeeRepository;
            _responsibilityRepository = responsibilityRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        // Personel sayfasını düzenleme (ekle/güncelle/sil) yetkisi: sadece Admin/Takım Lideri.
        // Görüntüleme (GetAll/GetAsync/GetCard) herkese açık kalır.
        private bool IsManager()
        {
            var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureManager()
        {
            if (!IsManager())
                throw new UserFriendlyException("Bu işlem için yetkiniz yok. Personel sayfasını yalnızca Admin/Takım Lideri düzenleyebilir.");
        }

        private long? CurrentEmployeeId()
        {
            var c = _httpContextAccessor.HttpContext?.User?.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        // Takım izolasyonu: Admin-self (Sistem Yöneticisi) TÜMÜNÜ görür; non-admin VEYA login-as ile
        // başka kişi olarak işlem yapan admin → o kişinin TAKIMINA kısıtlanır (başka takım/kişi görünmez).
        private async Task<(bool scope, long? teamId)> TeamScopeAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
            long? ownId = long.TryParse(user?.FindFirst("AdminOwnEmployeeId")?.Value, out var o) ? o : (long?)null;
            bool isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            // Admin-self: config-admin kendi kimliğinde VEYA AdminOwnEmployeeId claim'i olmayan (Google) admin → tümünü görür.
            bool adminSelf = isAdmin && (!empId.HasValue || !ownId.HasValue || empId == ownId);
            if (adminSelf || !empId.HasValue) return (false, null);
            var teamId = await _employeeRepository.GetAll()
                .Where(x => x.Id == empId.Value).Select(x => x.TeamId).FirstOrDefaultAsync();
            return (true, teamId);
        }

        public async Task<PagedResultDto<EmployeeDto>> GetAllAsync(GetEmployeesInput input)
        {
            var query = _employeeRepository.GetAll()
                .Include(e => e.Team)
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter),
                    e => e.FirstName.Contains(input.Filter) || e.LastName.Contains(input.Filter) ||
                         e.Department.Contains(input.Filter) || e.Title.Contains(input.Filter))
                .WhereIf(!string.IsNullOrWhiteSpace(input.Department),
                    e => e.Department == input.Department)
                .WhereIf(input.IsActive.HasValue, e => e.IsActive == input.IsActive.Value);

            var (scope, teamId) = await TeamScopeAsync();
            if (scope) query = query.Where(e => e.TeamId == teamId); // yalnız kendi takımının personeli

            var count = await query.CountAsync();
            var items = await query
                .OrderBy(e => e.LastName)
                .PageBy(input)
                .ToListAsync();

            var dtos = ObjectMapper.Map<List<EmployeeDto>>(items);
            for (int i = 0; i < items.Count; i++) dtos[i].TeamName = items[i].Team?.Name;
            return new PagedResultDto<EmployeeDto>(count, dtos);
        }

        public async Task<EmployeeDto> GetAsync(long id)
        {
            var employee = await _employeeRepository.GetAll().Include(e => e.Team).FirstOrDefaultAsync(e => e.Id == id);
            var d = ObjectMapper.Map<EmployeeDto>(employee);
            d.TeamName = employee?.Team?.Name;
            return d;
        }

        public async Task<EmployeeDto> GetCardAsync(long id)
        {
            var employee = await _employeeRepository.GetAll()
                .Include(e => e.Team).ThenInclude(t => t.Leader)
                .Include(e => e.Responsibilities)
                .Include(e => e.ProjectEmployees).ThenInclude(pe => pe.Project)
                .FirstOrDefaultAsync(e => e.Id == id);

            var dto = ObjectMapper.Map<EmployeeDto>(employee);
            dto.TeamName = employee.Team?.Name;
            dto.TeamLeaderId = employee.Team?.LeaderId;
            dto.TeamLeaderName = employee.Team?.Leader?.FullName;

            dto.Responsibilities = employee.Responsibilities
                .Where(r => r.IsActive)
                .OrderBy(r => r.OrderNo)
                .Select(r => new ResponsibilityDto { Id = r.Id, Title = r.Title, Description = r.Description, OrderNo = r.OrderNo })
                .ToList();

            // Projeler: hem takım üyesi (ProjectEmployee) hem de 1./2. sorumlu olduğu projeler birleştirilir.
            var assigned = new Dictionary<long, AssignedProjectDto>();
            foreach (var pe in employee.ProjectEmployees.Where(pe => pe.Project != null))
            {
                assigned[pe.ProjectId] = new AssignedProjectDto
                {
                    Id = pe.ProjectId,
                    Name = pe.Project.Name,
                    Code = pe.Project.Code,
                    Role = string.IsNullOrWhiteSpace(pe.Role) ? "Üye" : pe.Role,
                    Status = pe.Project.Status.ToString()
                };
            }

            var responsibleProjects = await _projectRepository.GetAll()
                .Where(p => p.PrimaryResponsibleId == id || p.SecondaryResponsibleId == id)
                .Select(p => new { p.Id, p.Name, p.Code, p.Status, p.PrimaryResponsibleId })
                .ToListAsync();
            foreach (var p in responsibleProjects)
            {
                var role = p.PrimaryResponsibleId == id ? "1. Sorumlu" : "2. Sorumlu";
                if (assigned.TryGetValue(p.Id, out var existing))
                {
                    // Sorumluluk rolü üyelikten önceliklidir
                    existing.Role = role;
                }
                else
                {
                    assigned[p.Id] = new AssignedProjectDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Code = p.Code,
                        Role = role,
                        Status = p.Status.ToString()
                    };
                }
            }

            dto.AssignedProjects = assigned.Values.OrderBy(x => x.Name).ToList();

            var tasks = await _taskRepository.GetAll()
                .Where(t => t.AssignedEmployeeId == id)
                .ToListAsync();

            dto.PendingTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.Beklemede);
            dto.InProgressTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.DevamEdiyor);
            dto.CompletedTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.Tamamlandi);

            return dto;
        }

        public async Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input)
        {
            EnsureManager();
            // E-posta girilmemişse isim.soyisim@cmit.com.tr olarak otomatik üret (Türkçe karakter → ASCII, benzersiz)
            if (string.IsNullOrWhiteSpace(input.Email))
                input.Email = await GenerateUniqueEmailAsync(input.FirstName, input.LastName);
            var employee = ObjectMapper.Map<Employee>(input);
            employee.TenantId = AbpSession.TenantId ?? 1;
            await _employeeRepository.InsertAsync(employee);
            await CurrentUnitOfWork.SaveChangesAsync();
            return ObjectMapper.Map<EmployeeDto>(employee);
        }

        private const string DefaultEmailDomain = "cmit.com.tr";

        // Türkçe karakterleri ASCII'ye çevirip küçük harfe indirger, harf/rakam dışını atar.
        private static string EmailSlug(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            const string tr = "çÇğĞıİöÖşŞüÜâÂîÎûÛ";
            const string en = "ccggiioossuuaaiiuu";
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s.Trim())
            {
                var idx = tr.IndexOf(ch);
                var c = idx >= 0 ? en[idx] : ch;
                if (c < 128 && char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private async Task<string> GenerateUniqueEmailAsync(string firstName, string lastName)
        {
            var local = $"{EmailSlug(firstName)}.{EmailSlug(lastName)}".Trim('.');
            if (string.IsNullOrEmpty(local)) local = "personel";
            var email = $"{local}@{DefaultEmailDomain}";
            int n = 1;
            while (await _employeeRepository.GetAll().IgnoreQueryFilters().AnyAsync(e => e.Email == email))
            {
                n++;
                email = $"{local}{n}@{DefaultEmailDomain}";
            }
            return email;
        }

        // Admin/Takım Lideri herkesi düzenleyebilir; diğerleri sadece kendi kaydını,
        // ve rol/aktiflik/hesap bağlantısı gibi hassas alanları değiştiremeden.
        public async Task<EmployeeDto> UpdateAsync(CreateUpdateEmployeeDto input)
        {
            var employee = await _employeeRepository.GetAsync(input.Id);
            bool isManager = IsManager();

            if (!isManager)
            {
                if (input.Id != CurrentEmployeeId())
                    throw new UserFriendlyException("Sadece kendi kaydınızı düzenleyebilirsiniz.");

                input.AppRole = employee.AppRole;
                input.IsActive = employee.IsActive;
                input.UserId = employee.UserId;
                input.TeamId = employee.TeamId;
            }

            ObjectMapper.Map(input, employee);
            await _employeeRepository.UpdateAsync(employee);
            return ObjectMapper.Map<EmployeeDto>(employee);
        }

        public async Task DeleteAsync(long id)
        {
            EnsureManager();
            await _employeeRepository.DeleteAsync(id);
        }

        public async Task UpdateRoleAsync(long id, string appRole)
        {
            var employee = await _employeeRepository.GetAsync(id);
            employee.AppRole = appRole;
            await _employeeRepository.UpdateAsync(employee);
        }

        public async Task<ListResultDto<EmployeeDto>> GetAllListAsync()
        {
            var employees = await _employeeRepository.GetAll()
                .Where(e => e.IsActive)
                .OrderBy(e => e.LastName)
                .ToListAsync();
            return new ListResultDto<EmployeeDto>(ObjectMapper.Map<List<EmployeeDto>>(employees));
        }
    }
}
