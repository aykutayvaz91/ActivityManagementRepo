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
        private readonly IRepository<ServiceRequest, long> _requestRepository;
        private readonly IRepository<TaskComment, long> _taskCommentRepository;
        private readonly IRepository<ServiceRequestComment, long> _requestCommentRepository;
        private readonly IRepository<ActivityLog, long> _logRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmployeeAppService(
            IRepository<Employee, long> employeeRepository,
            IRepository<Responsibility, long> responsibilityRepository,
            IRepository<ProjectEmployee, long> projectEmployeeRepository,
            IRepository<TaskItem, long> taskRepository,
            IRepository<Project, long> projectRepository,
            IRepository<ServiceRequest, long> requestRepository,
            IRepository<TaskComment, long> taskCommentRepository,
            IRepository<ServiceRequestComment, long> requestCommentRepository,
            IRepository<ActivityLog, long> logRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _employeeRepository = employeeRepository;
            _responsibilityRepository = responsibilityRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _requestRepository = requestRepository;
            _taskCommentRepository = taskCommentRepository;
            _requestCommentRepository = requestCommentRepository;
            _logRepository = logRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        // (Handover) Kişi izne çıkınca / pasife alınınca / silinince AÇIK işleri 2. sorumluya (yedek) devredilir;
        // her devirde iç not bırakılır ("neden" belli olsun). Yedek yoksa iş elde bırakılır + uyarı notu düşülür.
        // Dönüş: devir özet metni (kullanıcıya bilgi için).
        private async Task<string> HandoverOpenWorkAsync(Employee person, string reason)
        {
            if (person == null) return null;
            // İsim haritası (tek sorgu)
            var nameMap = await _employeeRepository.GetAll().AsNoTracking()
                .Select(e => new { e.Id, e.FirstName, e.LastName })
                .ToDictionaryAsync(e => e.Id, e => (e.FirstName + " " + e.LastName).Trim());
            string NameOf(long? id) => id.HasValue && nameMap.TryGetValue(id.Value, out var n) ? n : "?";
            var stamp = DateTime.Now.ToString("dd.MM.yyyy");
            int movedTasks = 0, orphanTasks = 0, movedReqs = 0, orphanReqs = 0;

            // AÇIK GÖREVLER (tamamlanmamış/iptal olmamış), 1. sorumlu = kişi
            var tasks = await _taskRepository.GetAll()
                .Where(t => t.AssignedEmployeeId == person.Id
                            && t.Status != Entities.TaskStatus.Tamamlandi
                            && t.Status != Entities.TaskStatus.Kapatildi
                            && t.Status != Entities.TaskStatus.Iptal)
                .ToListAsync();
            foreach (var t in tasks)
            {
                if (t.SecondaryEmployeeId.HasValue && t.SecondaryEmployeeId.Value != person.Id)
                {
                    var sec = t.SecondaryEmployeeId.Value;
                    t.AssignedEmployeeId = sec;           // 2. sorumlu ana sorumlu olur
                    t.SecondaryEmployeeId = person.Id;    // kişi yedeğe geçer
                    await _taskCommentRepository.InsertAsync(new TaskComment
                    {
                        TenantId = AbpSession.TenantId ?? 1,
                        TaskItemId = t.Id, IsInternal = true, AuthorName = "Sistem",
                        Comment = $"[{reason}] {person.FullName} nedeniyle görev 2. sorumlu {NameOf(sec)} kişisine devredildi ({stamp})."
                    });
                    movedTasks++;
                }
                else
                {
                    await _taskCommentRepository.InsertAsync(new TaskComment
                    {
                        TenantId = AbpSession.TenantId ?? 1,
                        TaskItemId = t.Id, IsInternal = true, AuthorName = "Sistem",
                        Comment = $"[{reason}] {person.FullName} — tanımlı yedek (2. sorumlu) yok; görev ELDE kaldı, elle yeniden atanmalı ({stamp})."
                    });
                    orphanTasks++;
                }
            }

            // AÇIK TALEPLER (kapanmamış/iptal olmamış), 1. sorumlu = kişi
            var reqs = await _requestRepository.GetAll()
                .Where(r => r.AssignedEmployeeId == person.Id
                            && r.Status != RequestStatus.Kapandi
                            && r.Status != RequestStatus.Iptal)
                .ToListAsync();
            foreach (var r in reqs)
            {
                if (r.SecondaryEmployeeId.HasValue && r.SecondaryEmployeeId.Value != person.Id)
                {
                    var sec = r.SecondaryEmployeeId.Value;
                    r.AssignedEmployeeId = sec;
                    r.SecondaryEmployeeId = person.Id;
                    await _requestCommentRepository.InsertAsync(new ServiceRequestComment
                    {
                        TenantId = r.TenantId, ServiceRequestId = r.Id, IsInternal = true,
                        AuthorName = "Sistem", CommentDate = DateTime.Now,
                        Body = $"[{reason}] {person.FullName} nedeniyle talep 2. sorumlu {NameOf(sec)} kişisine devredildi ({stamp})."
                    });
                    movedReqs++;
                }
                else
                {
                    await _requestCommentRepository.InsertAsync(new ServiceRequestComment
                    {
                        TenantId = r.TenantId, ServiceRequestId = r.Id, IsInternal = true,
                        AuthorName = "Sistem", CommentDate = DateTime.Now,
                        Body = $"[{reason}] {person.FullName} — yedek yok; talep ELDE kaldı, elle atanmalı ({stamp})."
                    });
                    orphanReqs++;
                }
            }

            await CurrentUnitOfWork.SaveChangesAsync();
            if (movedTasks + orphanTasks + movedReqs + orphanReqs == 0) return null;
            var parts = new System.Collections.Generic.List<string>();
            if (movedTasks > 0) parts.Add($"{movedTasks} görev yedeğe devredildi");
            if (movedReqs > 0) parts.Add($"{movedReqs} talep yedeğe devredildi");
            if (orphanTasks > 0) parts.Add($"{orphanTasks} görev yedeksiz (elde kaldı)");
            if (orphanReqs > 0) parts.Add($"{orphanReqs} talep yedeksiz (elde kaldı)");
            return $"{person.FullName}: " + string.Join(", ", parts) + ".";
        }

        // Kişi bugün fiilen izinli mi (işaret + tarih aralığı)
        private static bool IsOnLeaveNow(Employee e) =>
            e != null && e.IsOnLeave
            && (!e.LeaveStartDate.HasValue || e.LeaveStartDate.Value.Date <= DateTime.Today)
            && (!e.LeaveEndDate.HasValue || e.LeaveEndDate.Value.Date >= DateTime.Today);

        // Personel sayfasını düzenleme (ekle/güncelle/sil) yetkisi: sadece Admin/Takım Lideri.
        // Görüntüleme (GetAll/GetAsync/GetCard) herkese açık kalır.
        private bool IsManager()
        {
            var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAdmin()
        {
            var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureManager()
        {
            if (!IsManager())
                throw new UserFriendlyException("Bu işlem için yetkiniz yok. Personel sayfasını yalnızca Admin/Takım Lideri düzenleyebilir.");
        }

        private void EnsureAdmin()
        {
            if (!IsAdmin())
                throw new UserFriendlyException("Bu işlem yalnızca Admin tarafından yapılabilir.");
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
            // Manager: tüm takımları görür (admin gibi geniş görünürlük).
            if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase)) return (false, null);
            bool isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            // Admin-self: config-admin kendi kimliğinde VEYA AdminOwnEmployeeId claim'i olmayan (Google) admin → tümünü görür.
            bool adminSelf = isAdmin && (!empId.HasValue || !ownId.HasValue || empId == ownId);
            if (adminSelf || !empId.HasValue) return (false, null);
            // İşlem yapılan/temsil edilen kişinin (login-as dahil) gerçek rolüne bak:
            // Manager/Admin ise TÜM takımları görür (rol claim'i Admin kalsa da, o kişi Manager'sa Manager gibi görür).
            var emp = await _employeeRepository.GetAll()
                .Where(x => x.Id == empId.Value).Select(x => new { x.TeamId, x.AppRole }).FirstOrDefaultAsync();
            if (emp != null && (string.Equals(emp.AppRole, "Manager", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(emp.AppRole, "Admin", StringComparison.OrdinalIgnoreCase)))
                return (false, null);
            return (true, emp?.TeamId);
        }

        public async Task<PagedResultDto<EmployeeDto>> GetAllAsync(GetEmployeesInput input)
        {
            var query = _employeeRepository.GetAll().AsNoTracking()
                .Include(e => e.Team)
                .Where(e => !e.IsSystemAccount) // Sistem Yöneticisi personel listesinde/sayımında görünmez
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
            var employee = await _employeeRepository.GetAll().AsNoTracking().Include(e => e.Team).FirstOrDefaultAsync(e => e.Id == id);
            var d = ObjectMapper.Map<EmployeeDto>(employee);
            d.TeamName = employee?.Team?.Name;
            return d;
        }

        public async Task<EmployeeDto> GetCardAsync(long id)
        {
            var employee = await _employeeRepository.GetAll().AsNoTracking()
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

            var responsibleProjects = await _projectRepository.GetAll().AsNoTracking()
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

            var tasks = await _taskRepository.GetAll().AsNoTracking()
                .Where(t => t.AssignedEmployeeId == id)
                .ToListAsync();

            dto.PendingTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.Beklemede);
            dto.InProgressTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.DevamEdiyor);
            dto.CompletedTaskCount = tasks.Count(t => t.Status == Entities.TaskStatus.Tamamlandi);

            // Efor trendi: son 12 ay, aylık toplam saat (ActivityLog). Eksik aylar 0 ile doldurulur.
            var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
            var monthly = await _logRepository.GetAll().AsNoTracking()
                .Where(l => l.EmployeeId == id && l.ActivityDate >= firstMonth)
                .GroupBy(l => new { l.ActivityDate.Year, l.ActivityDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Hours = g.Sum(x => x.HoursSpent) })
                .ToListAsync();
            var trend = new List<EffortTrendPointDto>();
            for (int i = 0; i < 12; i++)
            {
                var m = firstMonth.AddMonths(i);
                var hit = monthly.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                trend.Add(new EffortTrendPointDto
                {
                    Label = m.ToString("MM.yyyy"),
                    Hours = hit == null ? 0m : Math.Round(hit.Hours, 1)
                });
            }
            dto.EffortTrend = trend;

            return dto;
        }

        public async Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input)
        {
            EnsureManager();
            // Rol atama yalnız Admin'e özel: Admin olmayan (TakımLideri) yeni personeli daima "Uzman" oluşturur
            // (aksi halde kendi kontrolündeki e-posta ile Admin personel açıp yetki yükseltebilirdi).
            if (!IsAdmin()) input.AppRole = "Uzman";
            // Manager admin gibi TÜM takımları görür → hiçbir takıma bağlanmaz (takımsız kalır).
            if (string.Equals(input.AppRole, "Manager", StringComparison.OrdinalIgnoreCase)) input.TeamId = null;
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
            bool isAdmin = IsAdmin();

            // Yönetici olmayan yalnız KENDİ kaydını düzenler.
            if (!isManager && input.Id != CurrentEmployeeId())
                throw new UserFriendlyException("Sadece kendi kaydınızı düzenleyebilirsiniz.");

            // GÜVENLİK: TakımLideri yalnız KENDİ takımının personelini düzenleyebilir (Admin/Manager tümü).
            if (isManager)
            {
                var (scoped, teamId) = await TeamScopeAsync();
                if (scoped && employee.TeamId != teamId)
                    throw new UserFriendlyException("Yalnızca kendi takımınızdaki personeli düzenleyebilirsiniz.");
            }

            // HASSAS alanlar (rol/aktiflik/hesap bağlantısı/takım) yalnız ADMIN tarafından değiştirilebilir.
            // (TakımLideri de dahil olmak üzere Admin olmayan hiç kimse AppRole'ü "Admin" yapıp yetki yükseltemez.)
            if (!isAdmin)
            {
                input.AppRole = employee.AppRole;
                input.IsActive = employee.IsActive;
                input.UserId = employee.UserId;
                input.TeamId = employee.TeamId;
            }

            // Manager admin gibi TÜM takımları görür → takımsız kalır (rol Manager ise takım bağını kaldır).
            if (string.Equals(input.AppRole, "Manager", StringComparison.OrdinalIgnoreCase)) input.TeamId = null;

            // DEVİR (handover) için ÖNCEKİ durumu yakala.
            bool wasOnLeaveNow = IsOnLeaveNow(employee);
            bool wasActive = employee.IsActive;

            ObjectMapper.Map(input, employee);
            await _employeeRepository.UpdateAsync(employee);

            // Geçiş: (a) yeni fiilen izinli oldu, (b) aktifken pasife alındı → açık işleri yedeğe devret + iz bırak.
            string handoverInfo = null;
            if (!wasOnLeaveNow && IsOnLeaveNow(employee))
                handoverInfo = await HandoverOpenWorkAsync(employee, "İzin");
            else if (wasActive && !employee.IsActive)
                handoverInfo = await HandoverOpenWorkAsync(employee, "Personel pasife alındı");

            var dto = ObjectMapper.Map<EmployeeDto>(employee);
            dto.HandoverInfo = handoverInfo; // controller TempData ile kullanıcıya bildirir
            return dto;
        }

        public async Task DeleteAsync(long id)
        {
            EnsureManager();
            // GÜVENLİK: TakımLideri yalnız KENDİ takımının personelini silebilir (Admin/Manager tümü).
            var target = await _employeeRepository.GetAsync(id);
            var (scoped, teamId) = await TeamScopeAsync();
            if (scoped && target.TeamId != teamId)
                throw new UserFriendlyException("Yalnızca kendi takımınızdaki personeli silebilirsiniz.");
            // AYRILMA/SİLME: silmeden ÖNCE açık işleri yedeğe devret + iz bırak.
            await HandoverOpenWorkAsync(target, "Personel silindi/ayrıldı");
            await _employeeRepository.DeleteAsync(id);
        }

        public async Task UpdateRoleAsync(long id, string appRole)
        {
            EnsureAdmin(); // rol atama yalnız Admin (API'den doğrudan çağrıya karşı korunur)
            var employee = await _employeeRepository.GetAsync(id);
            employee.AppRole = appRole;
            // Manager admin gibi TÜM takımları görür → takımsız kalır.
            if (string.Equals(appRole, "Manager", StringComparison.OrdinalIgnoreCase)) employee.TeamId = null;
            await _employeeRepository.UpdateAsync(employee);
        }

        public async Task<ListResultDto<EmployeeDto>> GetAllListAsync()
        {
            // Sistem Yöneticisi (config-admin) atama/sorumlu dropdown'larında GÖSTERİLMEZ.
            var employees = await _employeeRepository.GetAll().AsNoTracking()
                .Where(e => e.IsActive && !e.IsSystemAccount)
                .OrderBy(e => e.LastName)
                .ToListAsync();
            var dtos = ObjectMapper.Map<List<EmployeeDto>>(employees);

            // İş yükü göstergesi (H6): kişi başına AÇIK görev sayısı — tek GroupBy sorgusu.
            var openLoad = await _taskRepository.GetAll().AsNoTracking()
                .Where(t => t.AssignedEmployeeId.HasValue
                            && t.Status != Entities.TaskStatus.Tamamlandi
                            && t.Status != Entities.TaskStatus.Kapatildi
                            && t.Status != Entities.TaskStatus.Iptal)
                .GroupBy(t => t.AssignedEmployeeId.Value)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);
            foreach (var d in dtos)
                if (openLoad.TryGetValue(d.Id, out var c)) d.OpenTaskCount = c;

            return new ListResultDto<EmployeeDto>(dtos);
        }
    }
}
