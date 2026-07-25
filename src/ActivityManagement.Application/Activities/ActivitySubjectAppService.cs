using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Activities
{
    // Faaliyet Konusu yönetimi. Lider/Admin konu tanımlar ve uzmana atar; uzman efor girer.
    // Yetki kontrolleri (TaskItemAppService gibi) cookie claim'lerinden manuel yapılır.
    public class ActivitySubjectAppService : ActivityManagementAppServiceBase, IActivitySubjectAppService
    {
        private readonly IRepository<ActivitySubject, long> _subjectRepository;
        private readonly IRepository<ActivityLog, long> _logRepository;
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IRepository<SubCategory, long> _subCategoryRepository;
        private readonly IRepository<Project, long> _projectRepository;
        private readonly IRepository<SubCategoryResponsibility, long> _responsibilityRepository;
        private readonly IRepository<TaskItem, long> _taskRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ActivitySubjectAppService(
            IRepository<ActivitySubject, long> subjectRepository,
            IRepository<ActivityLog, long> logRepository,
            IRepository<Employee, long> employeeRepository,
            IRepository<SubCategory, long> subCategoryRepository,
            IRepository<Project, long> projectRepository,
            IRepository<SubCategoryResponsibility, long> responsibilityRepository,
            IRepository<TaskItem, long> taskRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _subjectRepository = subjectRepository;
            _logRepository = logRepository;
            _employeeRepository = employeeRepository;
            _subCategoryRepository = subCategoryRepository;
            _projectRepository = projectRepository;
            _responsibilityRepository = responsibilityRepository;
            _taskRepository = taskRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        // Parti 1c: Bir görevin ActualHours'unu ActivityLog toplamıyla senkronlar (efor eklenince/silinince/değişince).
        private async Task RecomputeTaskHoursAsync(long? taskItemId)
        {
            if (!taskItemId.HasValue) return;
            var sum = await _logRepository.GetAll()
                .Where(l => l.TaskItemId == taskItemId.Value)
                .Select(l => (decimal?)l.HoursSpent).SumAsync() ?? 0m;
            var task = await _taskRepository.FirstOrDefaultAsync(taskItemId.Value);
            if (task != null && task.ActualHours != sum)
            {
                task.ActualHours = sum;
                await CurrentUnitOfWork.SaveChangesAsync();
            }
        }

        // Proje seçildiyse faaliyet kategorilerini projeden doldur (override).
        private async Task ApplyProjectCategoryAsync(CreateUpdateActivitySubjectDto input)
        {
            if (!input.ProjectId.HasValue) return;
            var proj = await _projectRepository.GetAll().AsNoTracking()
                .Where(p => p.Id == input.ProjectId.Value)
                .Select(p => new { p.CategoryId, p.SubCategoryId })
                .FirstOrDefaultAsync();
            if (proj != null)
            {
                if (proj.CategoryId.HasValue) input.CategoryId = proj.CategoryId;
                if (proj.SubCategoryId.HasValue) input.SubCategoryId = proj.SubCategoryId;
            }
        }

        private (string Role, string Email, long? EmployeeId) CurrentContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            var email = user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.FindFirst(ClaimTypes.Name)?.Value;
            var empIdStr = user?.FindFirst("EmployeeId")?.Value;
            long? empId = long.TryParse(empIdStr, out var parsed) ? parsed : (long?)null;
            return (role, email, empId);
        }

        private static bool IsManager(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);

        private static bool IsAdmin(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        // Admin-self mi (Sistem Yöneticisi)? Login-as ile başka kişiye geçmişse false → takım kapsamı uygulanır.
        private bool IsAdminSelfContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return false;
            long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
            long? ownId = long.TryParse(user?.FindFirst("AdminOwnEmployeeId")?.Value, out var o) ? o : (long?)null;
            // config-admin kendi kimliğinde VEYA AdminOwnEmployeeId olmayan (Google) admin → self (tümünü görür)
            return !empId.HasValue || !ownId.HasValue || empId == ownId;
        }

        // Mevcut kullanıcının takımı — istek başına bir kez sorgulanır (cache'lenir)
        private bool _teamIdLoaded;
        private long? _currentTeamId;
        private long? CurrentEmployeeTeamId(long? employeeId)
        {
            if (_teamIdLoaded) return _currentTeamId;
            _teamIdLoaded = true;
            if (employeeId.HasValue)
                _currentTeamId = _employeeRepository.GetAll()
                    .Where(e => e.Id == employeeId.Value).Select(e => e.TeamId).FirstOrDefault();
            return _currentTeamId;
        }

        // Yönetici bu faaliyet konusunu YÖNETEBİLİR mi: Admin her zaman; TakımLideri yalnız kendi takımının
        // (takımsız da dahil) konusu. Uzman için false (kendi kaydı ayrı kontrol edilir).
        private bool IsManagerForSubject(ActivitySubject s, (string Role, string Email, long? EmployeeId) ctx)
        {
            if (!IsManager(ctx.Role)) return false;
            if (IsAdmin(ctx.Role)) return true;
            var myTeamId = CurrentEmployeeTeamId(ctx.EmployeeId);
            return !s.TeamId.HasValue || s.TeamId == myTeamId;
        }

        private IQueryable<ActivitySubject> WithIncludes(IQueryable<ActivitySubject> q) =>
            q.Include(s => s.Category)
             .Include(s => s.SubCategory).ThenInclude(sc => sc.Category)
             .Include(s => s.CreatedByLeader)
             .Include(s => s.AssignedEmployee)
             .Include(s => s.Team)
             .Include(s => s.Project)
             .Include(s => s.Logs);

        public async Task<List<ActivitySubjectDto>> GetAllAsync(GetActivitySubjectsInput input)
        {
            var ctx = CurrentContext();
            var query = WithIncludes(_subjectRepository.GetAll().AsNoTracking())
                .WhereIf(input.CategoryId.HasValue, s => s.CategoryId == input.CategoryId.Value)
                .WhereIf(input.SubCategoryId.HasValue, s => s.SubCategoryId == input.SubCategoryId.Value)
                .WhereIf(input.AssignedEmployeeId.HasValue, s => s.AssignedEmployeeId == input.AssignedEmployeeId.Value)
                .WhereIf(input.ProjectId.HasValue, s => s.ProjectId == input.ProjectId.Value)
                .WhereIf(input.OnlyActive == true, s => s.IsActive);

            // Görünürlük kuralı: Admin tüm konuları görür; TakımLideri ve Uzman kendi TAKIMININ
            // konularını görür (takımdaki kişilere atanan/oluşturulan faaliyetler dahil). İşlem yetkisi
            // (efor/düzenle/sil) ayrıca "kendine ait" kuralıyla sınırlıdır (CanManage/CanLogEffort).
            if (!IsAdminSelfContext() && ctx.EmployeeId.HasValue)
            {
                var myTeamId = await _employeeRepository.GetAll().AsNoTracking()
                    .Where(e => e.Id == ctx.EmployeeId.Value).Select(e => e.TeamId).FirstOrDefaultAsync();
                query = query.Where(s =>
                    (myTeamId != null && s.TeamId == myTeamId) ||
                    s.AssignedEmployeeId == ctx.EmployeeId.Value ||
                    s.CreatedByLeaderId == ctx.EmployeeId.Value);
            }

            var items = await query.OrderByDescending(s => s.CreationTime).ToListAsync();
            return items.Select(s => MapSubject(s, ctx)).ToList();
        }

        // Proje detayında projenin TÜM faaliyetleri gösterilir (kişisel/takım kapsamına takılmadan).
        // Proje detayını görüntüleme yetkisi controller seviyesinde (giriş zorunlu) sağlanır.
        public async Task<List<ActivitySubjectDto>> GetByProjectAsync(long projectId)
        {
            var ctx = CurrentContext();
            var items = await WithIncludes(_subjectRepository.GetAll().AsNoTracking())
                .Where(s => s.ProjectId == projectId)
                .OrderByDescending(s => s.CreationTime)
                .ToListAsync();
            return items.Select(s => MapSubject(s, ctx)).ToList();
        }

        public async Task<ActivitySubjectDto> GetAsync(long id)
        {
            var ctx = CurrentContext();
            var s = await WithIncludes(_subjectRepository.GetAll().AsNoTracking())
                .FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) throw new UserFriendlyException("Faaliyet konusu bulunamadı.");
            return MapSubject(s, ctx);
        }

        public async Task<ActivitySubjectDto> CreateAsync(CreateUpdateActivitySubjectDto input)
        {
            var ctx = CurrentContext();
            // Admin, Takım Lideri ve Uzman — 3 rol de onaysız faaliyet konusu açabilir.
            if (string.IsNullOrWhiteSpace(input.Title))
                throw new UserFriendlyException("Faaliyet konusu başlığı zorunludur.");

            // Uzman yalnızca kendi adına konu açar → konu kendisine atanır (altına efor girebilsin).
            // Yönetici (Admin/Lider) dilediği uzmana atar.
            if (!IsManager(ctx.Role))
                input.AssignedEmployeeId = ctx.EmployeeId;

            await ApplyProjectCategoryAsync(input); // proje seçiliyse kategoriler projeden

            // Faaliyet projesiz olabilir ama KATEGORİSİZ olamaz (proje seçilirse kategori projeden dolar).
            if (!input.CategoryId.HasValue && !input.SubCategoryId.HasValue)
                throw new UserFriendlyException("Faaliyet konusu için kategori zorunludur (proje seçilirse otomatik dolar).");

            var subject = new ActivitySubject
            {
                TenantId = AbpSession.TenantId ?? 1,
                Title = input.Title,
                Description = input.Description,
                ActivityType = input.ActivityType,
                SubCategoryId = input.SubCategoryId,
                CategoryId = await ResolveCategoryIdAsync(input),
                ProjectId = input.ProjectId,
                AssignedEmployeeId = input.AssignedEmployeeId,
                CreatedByLeaderId = ctx.EmployeeId,
                TeamId = await ResolveTeamIdAsync(input.AssignedEmployeeId, ctx.EmployeeId),
                IsActive = input.IsActive
            };
            await _subjectRepository.InsertAsync(subject);
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(subject.Id);
        }

        public async Task<ActivitySubjectDto> UpdateAsync(CreateUpdateActivitySubjectDto input)
        {
            var ctx = CurrentContext();
            var subject = await _subjectRepository.GetAsync(input.Id);
            EnsureCanManage(subject, ctx);

            await ApplyProjectCategoryAsync(input);
            if (!input.CategoryId.HasValue && !input.SubCategoryId.HasValue)
                throw new UserFriendlyException("Faaliyet konusu için kategori zorunludur (proje seçilirse otomatik dolar).");
            subject.Title = input.Title;
            subject.Description = input.Description;
            subject.ActivityType = input.ActivityType;
            subject.SubCategoryId = input.SubCategoryId;
            subject.CategoryId = await ResolveCategoryIdAsync(input);
            subject.ProjectId = input.ProjectId;
            subject.AssignedEmployeeId = input.AssignedEmployeeId;
            subject.TeamId = await ResolveTeamIdAsync(input.AssignedEmployeeId, subject.CreatedByLeaderId);
            subject.IsActive = input.IsActive;
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(subject.Id);
        }

        public async Task DeleteAsync(long id)
        {
            var ctx = CurrentContext();
            var subject = await _subjectRepository.GetAsync(id);
            EnsureCanManage(subject, ctx);
            await _subjectRepository.DeleteAsync(id);
        }

        public async Task<ActivityLogDto> LogEffortAsync(CreateActivityLogDto input)
        {
            var ctx = CurrentContext();
            if (!input.ActivitySubjectId.HasValue)
                throw new UserFriendlyException("Efor girişi için faaliyet konusu gereklidir.");

            var subject = await _subjectRepository.GetAll().AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == input.ActivitySubjectId.Value);
            if (subject == null) throw new UserFriendlyException("Faaliyet konusu bulunamadı.");

            // Efor yalnızca faaliyet konusunun ATANAN kişisi tarafından, kendi adına girilir.
            // (Yönetici/admin efor girmez; konuyu yalnızca düzenler.)
            if (!(subject.AssignedEmployeeId.HasValue && ctx.EmployeeId.HasValue && subject.AssignedEmployeeId == ctx.EmployeeId))
                throw new UserFriendlyException("Efor yalnızca faaliyet konusunun atanan kişisi tarafından girilebilir.");

            long employeeId = ctx.EmployeeId.Value; // kendi adına
            if (input.HoursSpent <= 0)
                throw new UserFriendlyException("Harcanan süre 0'dan büyük olmalıdır.");

            if (input.HoursSpent <= 0)
                throw new UserFriendlyException("Harcanan süre 0'dan büyük olmalıdır.");

            var log = new ActivityLog
            {
                TenantId = AbpSession.TenantId ?? 1,
                EmployeeId = employeeId,
                ActivitySubjectId = subject.Id,
                ProjectId = subject.ProjectId,   // #1: konu bir projeye bağlıysa efor da o projeye sayılır (raporlama)
                Description = input.Description,
                ActivityDate = input.ActivityDate == default ? DateTime.Today : input.ActivityDate,
                HoursSpent = input.HoursSpent,
                // Efor faaliyetin tipini devralır (raporlama). Form tip gönderdiyse o, yoksa konunun tipi, o da yoksa "Faaliyet".
                ActivityType = !string.IsNullOrWhiteSpace(input.ActivityType) ? input.ActivityType
                               : (!string.IsNullOrWhiteSpace(subject.ActivityType) ? subject.ActivityType : "Faaliyet")
            };
            await _logRepository.InsertAsync(log);
            await CurrentUnitOfWork.SaveChangesAsync();

            var saved = await _logRepository.GetAll().AsNoTracking()
                .Include(a => a.Employee)
                .Include(a => a.ActivitySubject)
                .FirstOrDefaultAsync(a => a.Id == log.Id);
            return MapLog(saved);
        }

        public async Task<List<ActivityLogDto>> GetEffortsAsync(long activitySubjectId)
        {
            var items = await _logRepository.GetAll().AsNoTracking()
                .Include(a => a.Employee)
                .Include(a => a.ActivitySubject)
                .Where(a => a.ActivitySubjectId == activitySubjectId)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();
            return items.Select(MapLog).ToList();
        }

        // Efor kaydı silme: kendi girdiği efor VEYA yönetici (Admin tümü, TakımLideri kendi takımının konusu) (#6).
        public async Task DeleteEffortAsync(long id)
        {
            var ctx = CurrentContext();
            var log = await _logRepository.FirstOrDefaultAsync(id);
            if (log == null) throw new UserFriendlyException("Efor kaydı bulunamadı.");

            bool canDelete = ctx.EmployeeId.HasValue && log.EmployeeId == ctx.EmployeeId.Value; // kendi eforu
            if (!canDelete && IsAdmin(ctx.Role)) canDelete = true;                              // Admin tümü
            if (!canDelete && IsManager(ctx.Role) && log.ActivitySubjectId.HasValue)            // TakımLideri: kendi takımı
            {
                var subj = await _subjectRepository.FirstOrDefaultAsync(log.ActivitySubjectId.Value);
                canDelete = subj != null && IsManagerForSubject(subj, ctx);
            }
            if (!canDelete)
                throw new UserFriendlyException("Bu efor kaydını silme yetkiniz yok.");
            var taskId = log.TaskItemId;
            await _logRepository.DeleteAsync(id);
            await CurrentUnitOfWork.SaveChangesAsync();
            await RecomputeTaskHoursAsync(taskId); // görev eforu silinince ActualHours düşsün
        }

        // V4: Günü 8 saate tamamla — kullanıcının sorumlu olduğu alt kategoriler için 1'er saatlik
        // "Rutin Kontrol" efor kaydı üretir; günlük toplam efor 8 saate ulaşana dek doldurur.
        public async Task<int> CompleteDayTo8HoursAsync(DateTime? date = null)
        {
            var ctx = CurrentContext();
            if (!ctx.EmployeeId.HasValue)
                throw new UserFriendlyException("Efor girişi için personel kaydınız bulunmuyor.");

            var day = (date ?? DateTime.Today).Date;
            var next = day.AddDays(1);

            var existing = await _logRepository.GetAll()
                .Where(a => a.EmployeeId == ctx.EmployeeId.Value && a.ActivityDate >= day && a.ActivityDate < next)
                .Select(a => (decimal?)a.HoursSpent).SumAsync() ?? 0m;

            decimal remaining = 8m - existing;
            if (remaining < 1m)
                throw new UserFriendlyException($"Gün zaten {existing:0.##} saat efor içeriyor; tamamlamaya gerek yok.");

            // Öncelik: 1. SORUMLU (PrimaryResponsible) olunan AKTİF projeler → efor projeye bağlanır (ProjectId).
            var projects = await _projectRepository.GetAll().AsNoTracking()
                .Where(p => p.PrimaryResponsibleId == ctx.EmployeeId.Value
                         && p.Status != ProjectStatus.Tamamlandi && p.Status != ProjectStatus.Iptal)
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            // Proje yoksa: 1. sorumlu olunan alt kategoriler (yalnız açıklama; projesiz)
            var subs = new List<string>();
            if (!projects.Any())
            {
                subs = await _responsibilityRepository.GetAll()
                    .Include(r => r.SubCategory)
                    .Where(r => r.EmployeeId == ctx.EmployeeId.Value && r.ResponsibilityType == ResponsibilityType.Primary)
                    .Select(r => r.SubCategory.Name)
                    .ToListAsync();
                if (!subs.Any()) subs = new List<string> { "Genel sistem rutin kontrolü" };
            }

            int created = 0, i = 0;
            while (remaining >= 1m && created < 24)
            {
                var log = new ActivityLog
                {
                    TenantId = AbpSession.TenantId ?? 1,
                    EmployeeId = ctx.EmployeeId.Value,
                    ActivityDate = day,
                    HoursSpent = 1m,
                    ActivityType = "Rutin Kontrol"
                };
                if (projects.Any())
                {
                    var pr = projects[i % projects.Count];
                    log.ProjectId = pr.Id;                                   // efor projeye sayılır
                    log.Description = $"Rutin kontrol: {pr.Name}";
                }
                else
                {
                    var name = subs[i % subs.Count];
                    log.Description = $"Rutin sistem kontrolü (1. sorumlu): {name}";
                }
                await _logRepository.InsertAsync(log);
                remaining -= 1m; created++; i++;
            }
            await CurrentUnitOfWork.SaveChangesAsync();
            return created;
        }

        // V4/R1: Seçili günün efor kayıtları + toplam + eksik (8 saate göre) — kendi kaydı.
        public async Task<DayEffortDto> GetDayEffortsAsync(DateTime? date = null)
        {
            var ctx = CurrentContext();
            var day = (date ?? DateTime.Today).Date;
            var next = day.AddDays(1);
            var result = new DayEffortDto { Date = day };
            if (!ctx.EmployeeId.HasValue) return result;

            var logs = await _logRepository.GetAll().AsNoTracking()
                .Include(a => a.Employee).Include(a => a.ActivitySubject).Include(a => a.TaskItem).Include(a => a.Project).Include(a => a.ServiceRequest)
                .Where(a => a.EmployeeId == ctx.EmployeeId.Value && a.ActivityDate >= day && a.ActivityDate < next)
                .OrderBy(a => a.Id)
                .ToListAsync();

            result.Efforts = logs.Select(a => new ActivityLogDto
            {
                Id = a.Id,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.Employee?.FullName,
                ActivitySubjectId = a.ActivitySubjectId,
                ActivitySubjectTitle = a.ActivitySubject?.Title,
                ServiceRequestId = a.ServiceRequestId,
                ServiceRequestTitle = a.ServiceRequest?.Title,
                TaskItemId = a.TaskItemId,
                TaskTitle = a.TaskItem?.Title,
                ProjectId = a.ProjectId,
                ProjectName = a.Project?.Name,
                Description = a.Description,
                ActivityDate = a.ActivityDate,
                HoursSpent = a.HoursSpent,
                ActivityType = a.ActivityType
            }).ToList();

            result.TotalHours = result.Efforts.Sum(e => e.HoursSpent);
            result.MissingHours = System.Math.Max(0m, 8m - result.TotalHours);
            return result;
        }

        // R1: Serbest (manuel) günlük efor ekleme — proje/görev opsiyonel. Proje seçilirse efor projeye sayılır.
        public async Task AddManualEffortAsync(DateTime date, decimal hoursSpent, string description, string activityType, long? taskItemId = null, long? projectId = null, long? serviceRequestId = null)
        {
            var ctx = CurrentContext();
            if (!ctx.EmployeeId.HasValue)
                throw new UserFriendlyException("Efor girişi için personel kaydınız bulunmuyor.");
            if (hoursSpent <= 0)
                throw new UserFriendlyException("Harcanan süre 0'dan büyük olmalıdır.");

            await _logRepository.InsertAsync(new ActivityLog
            {
                TenantId = AbpSession.TenantId ?? 1,
                EmployeeId = ctx.EmployeeId.Value,
                ActivityDate = date.Date == default ? DateTime.Today : date.Date,
                HoursSpent = hoursSpent,
                ActivityType = string.IsNullOrWhiteSpace(activityType) ? "Faaliyet" : activityType,
                Description = description,
                TaskItemId = taskItemId,
                ProjectId = projectId,
                ServiceRequestId = serviceRequestId
            });
            await CurrentUnitOfWork.SaveChangesAsync();
            await RecomputeTaskHoursAsync(taskItemId); // görev eforu → ActualHours
        }

        // R1: Efor kaydı düzenleme (otomatik girilenler dahil) — kendi kaydı VEYA yönetici. Proje değiştirilebilir.
        public async Task UpdateEffortAsync(long id, decimal hoursSpent, string description, DateTime activityDate, string activityType, long? projectId = null)
        {
            var ctx = CurrentContext();
            var log = await _logRepository.GetAsync(id);
            // Kendi eforu VEYA Admin VEYA (TakımLideri ise) yalnız kendi TAKIMINDAKİ kişinin eforu.
            bool canEdit = (ctx.EmployeeId.HasValue && log.EmployeeId == ctx.EmployeeId.Value) || IsAdmin(ctx.Role);
            if (!canEdit && IsManager(ctx.Role))
            {
                var myTeam = CurrentEmployeeTeamId(ctx.EmployeeId);
                var logEmpTeam = await _employeeRepository.GetAll().AsNoTracking()
                    .Where(e => e.Id == log.EmployeeId).Select(e => e.TeamId).FirstOrDefaultAsync();
                canEdit = myTeam.HasValue && logEmpTeam == myTeam;
            }
            if (!canEdit)
                throw new UserFriendlyException("Bu efor kaydını düzenleme yetkiniz yok.");
            if (hoursSpent <= 0)
                throw new UserFriendlyException("Harcanan süre 0'dan büyük olmalıdır.");
            log.HoursSpent = hoursSpent;
            log.Description = description;
            if (activityDate != default) log.ActivityDate = activityDate.Date;
            if (!string.IsNullOrWhiteSpace(activityType)) log.ActivityType = activityType;
            log.ProjectId = projectId;   // proje ata/temizle
            await CurrentUnitOfWork.SaveChangesAsync();
            await RecomputeTaskHoursAsync(log.TaskItemId); // saat değişince görev ActualHours güncellensin
        }

        // --- yardımcılar ---

        private void EnsureCanManage(ActivitySubject subject, (string Role, string Email, long? EmployeeId) ctx)
        {
            bool canManage = IsManagerForSubject(subject, ctx) ||
                (subject.CreatedByLeaderId.HasValue && ctx.EmployeeId.HasValue && subject.CreatedByLeaderId == ctx.EmployeeId);
            if (!canManage)
                throw new UserFriendlyException("Bu faaliyet konusu üzerinde yetkiniz yok.");
        }

        private async Task<long?> ResolveCategoryIdAsync(CreateUpdateActivitySubjectDto input)
        {
            if (input.CategoryId.HasValue) return input.CategoryId;
            if (input.SubCategoryId.HasValue)
            {
                return await _subCategoryRepository.GetAll().AsNoTracking()
                    .Where(sc => sc.Id == input.SubCategoryId.Value)
                    .Select(sc => (long?)sc.CategoryId)
                    .FirstOrDefaultAsync();
            }
            return null;
        }

        private async Task<long?> ResolveTeamIdAsync(long? assignedEmployeeId, long? fallbackEmployeeId)
        {
            var empId = assignedEmployeeId ?? fallbackEmployeeId;
            if (!empId.HasValue) return null;
            return await _employeeRepository.GetAll().AsNoTracking()
                .Where(e => e.Id == empId.Value)
                .Select(e => e.TeamId)
                .FirstOrDefaultAsync();
        }

        private ActivitySubjectDto MapSubject(ActivitySubject s, (string Role, string Email, long? EmployeeId) ctx)
        {
            var dto = ObjectMapper.Map<ActivitySubjectDto>(s);
            dto.CategoryName = s.Category?.Name ?? s.SubCategory?.Category?.Name;
            dto.SubCategoryName = s.SubCategory?.Name;
            dto.ProjectName = s.Project?.Name;
            dto.CreatedByLeaderName = s.CreatedByLeader?.FullName;
            dto.AssignedEmployeeName = s.AssignedEmployee?.FullName;
            dto.TeamName = s.Team?.Name;
            dto.LogCount = s.Logs?.Count ?? 0;
            dto.TotalHours = s.Logs?.Sum(l => l.HoursSpent) ?? 0m;
            dto.CanManage = IsManagerForSubject(s, ctx) ||
                (s.CreatedByLeaderId.HasValue && ctx.EmployeeId.HasValue && s.CreatedByLeaderId == ctx.EmployeeId);
            // Efor yalnızca atanan kişi tarafından girilir (yönetici/admin efor girmez, sadece yönetir)
            dto.CanLogEffort = s.AssignedEmployeeId.HasValue && ctx.EmployeeId.HasValue && s.AssignedEmployeeId == ctx.EmployeeId;
            return dto;
        }

        private ActivityLogDto MapLog(ActivityLog a)
        {
            if (a == null) return null;
            var dto = ObjectMapper.Map<ActivityLogDto>(a);
            dto.EmployeeName = a.Employee?.FullName;
            dto.ActivitySubjectTitle = a.ActivitySubject?.Title;
            return dto;
        }
    }
}
