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
using ActivityManagement.ServiceRequests.Dto;

namespace ActivityManagement.ServiceRequests
{
    // Talep (ServiceRequest) yönetimi: psm.tdv.org (sunucu kurulum) ve destek.cmit.com.tr (dış destek)
    // portallarından gelen/elle girilen talepler. Görev/Faaliyet gibi eforlu iş. Yetki cookie claim'lerinden.
    public class ServiceRequestAppService : ActivityManagementAppServiceBase, IServiceRequestAppService
    {
        private readonly IRepository<ServiceRequest, long> _requestRepository;
        private readonly IRepository<ActivityLog, long> _logRepository;
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IRepository<Team, long> _teamRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ServiceRequestAppService(
            IRepository<ServiceRequest, long> requestRepository,
            IRepository<ActivityLog, long> logRepository,
            IRepository<Employee, long> employeeRepository,
            IRepository<Team, long> teamRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _requestRepository = requestRepository;
            _logRepository = logRepository;
            _employeeRepository = employeeRepository;
            _teamRepository = teamRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        // --- bağlam / yetki yardımcıları (diğer AppService'lerle aynı desen) ---

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

        // Admin-self mi? Login-as ile başka kişiye geçmişse false → takım kapsamı uygulanır.
        private bool IsAdminSelfContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return false;
            long? empId = long.TryParse(user?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
            long? ownId = long.TryParse(user?.FindFirst("AdminOwnEmployeeId")?.Value, out var o) ? o : (long?)null;
            return !empId.HasValue || !ownId.HasValue || empId == ownId;
        }

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

        // Yönetici bu talebi yönetebilir mi: Admin her zaman; TakımLideri yalnız kendi takımının (takımsız dahil).
        private bool IsManagerForRequest(ServiceRequest r, (string Role, string Email, long? EmployeeId) ctx)
        {
            if (!IsManager(ctx.Role)) return false;
            if (IsAdmin(ctx.Role)) return true;
            var myTeamId = CurrentEmployeeTeamId(ctx.EmployeeId);
            return !r.TeamId.HasValue || r.TeamId == myTeamId;
        }

        private IQueryable<ServiceRequest> WithIncludes(IQueryable<ServiceRequest> q) =>
            q.Include(r => r.AssignedEmployee)
             .Include(r => r.SecondaryEmployee)
             .Include(r => r.Team)
             .Include(r => r.Category)
             .Include(r => r.SubCategory)
             .Include(r => r.Project)
             .Include(r => r.Logs);

        // --- sorgular ---

        public async Task<List<ServiceRequestDto>> GetAllAsync(GetServiceRequestsInput input)
        {
            var ctx = CurrentContext();
            var query = WithIncludes(_requestRepository.GetAll().AsNoTracking())
                .WhereIf(input.Source.HasValue, r => r.Source == input.Source.Value)
                .WhereIf(input.Status.HasValue, r => r.Status == input.Status.Value)
                .WhereIf(input.AssignedEmployeeId.HasValue, r => r.AssignedEmployeeId == input.AssignedEmployeeId.Value)
                .WhereIf(input.OnlyOpen == true, r => r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal)
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), r =>
                    r.Title.Contains(input.Filter) ||
                    (r.ExternalRef != null && r.ExternalRef.Contains(input.Filter)) ||
                    (r.RequesterName != null && r.RequesterName.Contains(input.Filter)));

            // Yalnız bana atanan
            if (input.MineOnly == true && ctx.EmployeeId.HasValue)
                query = query.Where(r => r.AssignedEmployeeId == ctx.EmployeeId.Value ||
                                         r.SecondaryEmployeeId == ctx.EmployeeId.Value);

            // Görünürlük: Admin-self tümünü; diğerleri kendi TAKIMININ talepleri + kendine atananları.
            if (input.MineOnly != true && !IsAdminSelfContext() && ctx.EmployeeId.HasValue)
            {
                var myTeamId = await _employeeRepository.GetAll().AsNoTracking()
                    .Where(e => e.Id == ctx.EmployeeId.Value).Select(e => e.TeamId).FirstOrDefaultAsync();
                query = query.Where(r =>
                    (myTeamId != null && r.TeamId == myTeamId) ||
                    r.AssignedEmployeeId == ctx.EmployeeId.Value ||
                    r.SecondaryEmployeeId == ctx.EmployeeId.Value);
            }

            // Açık talepler önce; sonra önem skoru, sonra yakın SLA.
            var items = await query
                .OrderByDescending(r => r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal)
                .ThenByDescending(r => r.PriorityScore)
                .ThenBy(r => r.DueDate ?? DateTime.MaxValue)
                .ToListAsync();
            return items.Select(r => MapRequest(r, ctx)).ToList();
        }

        public async Task<ServiceRequestDto> GetAsync(long id)
        {
            var ctx = CurrentContext();
            var r = await WithIncludes(_requestRepository.GetAll().AsNoTracking())
                .FirstOrDefaultAsync(x => x.Id == id);
            if (r == null) throw new UserFriendlyException("Talep bulunamadı.");
            return MapRequest(r, ctx);
        }

        // --- yazma ---

        public async Task<ServiceRequestDto> CreateAsync(CreateUpdateServiceRequestDto input)
        {
            var ctx = CurrentContext();
            if (!IsManager(ctx.Role))
                throw new UserFriendlyException("Talep oluşturmaya yetkiniz yok.");
            if (string.IsNullOrWhiteSpace(input.Title))
                throw new UserFriendlyException("Talep başlığı zorunludur.");

            var entity = new ServiceRequest
            {
                TenantId = AbpSession.TenantId ?? 1,
                Source = input.Source,
                ExternalRef = string.IsNullOrWhiteSpace(input.ExternalRef) ? null : input.ExternalRef.Trim(),
                ExternalUrl = input.ExternalUrl,
                Title = input.Title,
                Description = input.Description,
                RequesterName = input.RequesterName,
                RequesterEmail = input.RequesterEmail,
                ExtraInfo = input.ExtraInfo,
                Priority = input.Priority,
                PriorityScore = ClampScore(input.PriorityScore),
                AssignedEmployeeId = input.AssignedEmployeeId,
                SecondaryEmployeeId = input.SecondaryEmployeeId,
                ProjectId = input.ProjectId,
                ReceivedDate = input.ReceivedDate ?? DateTime.Now,
                DueDate = input.DueDate,
                TeamId = await ResolveTeamIdAsync(input.AssignedEmployeeId, ctx.EmployeeId),
                Status = input.AssignedEmployeeId.HasValue ? RequestStatus.Atandi : RequestStatus.Yeni,
                CompletionPercentage = 0
            };
            await _requestRepository.InsertAsync(entity);
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(entity.Id);
        }

        public async Task<ServiceRequestDto> UpdateAsync(CreateUpdateServiceRequestDto input)
        {
            var ctx = CurrentContext();
            var r = await _requestRepository.GetAsync(input.Id);
            EnsureCanManage(r, ctx);

            r.Source = input.Source;
            r.ExternalRef = string.IsNullOrWhiteSpace(input.ExternalRef) ? null : input.ExternalRef.Trim();
            r.ExternalUrl = input.ExternalUrl;
            r.Title = input.Title;
            r.Description = input.Description;
            r.RequesterName = input.RequesterName;
            r.RequesterEmail = input.RequesterEmail;
            r.ExtraInfo = input.ExtraInfo;
            r.Priority = input.Priority;
            r.PriorityScore = ClampScore(input.PriorityScore);
            r.ProjectId = input.ProjectId;
            r.ReceivedDate = input.ReceivedDate ?? r.ReceivedDate;
            r.DueDate = input.DueDate;
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(r.Id);
        }

        public async Task DeleteAsync(long id)
        {
            var ctx = CurrentContext();
            var r = await _requestRepository.GetAsync(id);
            EnsureCanManage(r, ctx);
            await _requestRepository.DeleteAsync(id);
        }

        public async Task<ServiceRequestDto> AssignAsync(long id, long? assignedEmployeeId, long? secondaryEmployeeId = null)
        {
            var ctx = CurrentContext();
            var r = await _requestRepository.GetAsync(id);
            EnsureCanManage(r, ctx);

            r.AssignedEmployeeId = assignedEmployeeId;
            r.SecondaryEmployeeId = secondaryEmployeeId;
            r.TeamId = await ResolveTeamIdAsync(assignedEmployeeId, ctx.EmployeeId) ?? r.TeamId;
            if (assignedEmployeeId.HasValue && r.Status == RequestStatus.Yeni)
                r.Status = RequestStatus.Atandi;
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(r.Id);
        }

        public async Task<ServiceRequestDto> UpdateStatusAsync(long id, RequestStatus status, int percentage)
        {
            var ctx = CurrentContext();
            var r = await _requestRepository.GetAsync(id);

            bool canManage = IsManagerForRequest(r, ctx);
            bool isAssignee = r.AssignedEmployeeId.HasValue && ctx.EmployeeId.HasValue && r.AssignedEmployeeId == ctx.EmployeeId;
            if (!canManage && !isAssignee)
                throw new UserFriendlyException("Bu talebin durumunu güncelleme yetkiniz yok.");

            r.Status = status;
            r.CompletionPercentage = ProgressForStatus(status, percentage > 0 ? percentage : r.CompletionPercentage);
            r.ResolvedDate = status == RequestStatus.Cozuldu ? (r.ResolvedDate ?? DateTime.Now) : r.ResolvedDate;
            r.ClosedDate = status == RequestStatus.Kapandi ? DateTime.Now : (status == RequestStatus.Iptal ? DateTime.Now : null);
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(r.Id);
        }

        // --- efor ---

        public async Task<ActivityLogDto> LogEffortAsync(CreateActivityLogDto input)
        {
            var ctx = CurrentContext();
            if (!input.ServiceRequestId.HasValue)
                throw new UserFriendlyException("Efor girişi için talep gereklidir.");

            var r = await _requestRepository.GetAll().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == input.ServiceRequestId.Value);
            if (r == null) throw new UserFriendlyException("Talep bulunamadı.");

            // Efor yalnız talebin ATANAN kişisi tarafından, kendi adına girilir.
            if (!(r.AssignedEmployeeId.HasValue && ctx.EmployeeId.HasValue && r.AssignedEmployeeId == ctx.EmployeeId))
                throw new UserFriendlyException("Efor yalnızca talebin atanan kişisi tarafından girilebilir.");
            if (input.HoursSpent <= 0)
                throw new UserFriendlyException("Harcanan süre 0'dan büyük olmalıdır.");

            var log = new ActivityLog
            {
                TenantId = AbpSession.TenantId ?? 1,
                EmployeeId = ctx.EmployeeId.Value,
                ServiceRequestId = r.Id,
                ProjectId = r.ProjectId,   // talep bir projeye bağlıysa efor projeye de sayılır (raporlama)
                Description = input.Description,
                ActivityDate = input.ActivityDate == default ? DateTime.Today : input.ActivityDate,
                HoursSpent = input.HoursSpent,
                ActivityType = string.IsNullOrWhiteSpace(input.ActivityType) ? "Talep" : input.ActivityType
            };
            await _logRepository.InsertAsync(log);
            await CurrentUnitOfWork.SaveChangesAsync();

            var saved = await _logRepository.GetAll().AsNoTracking()
                .Include(a => a.Employee).Include(a => a.ServiceRequest)
                .FirstOrDefaultAsync(a => a.Id == log.Id);
            return MapLog(saved);
        }

        public async Task<List<ActivityLogDto>> GetEffortsAsync(long serviceRequestId)
        {
            var items = await _logRepository.GetAll().AsNoTracking()
                .Include(a => a.Employee).Include(a => a.ServiceRequest)
                .Where(a => a.ServiceRequestId == serviceRequestId)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();
            return items.Select(MapLog).ToList();
        }

        public async Task DeleteEffortAsync(long id)
        {
            var ctx = CurrentContext();
            var log = await _logRepository.FirstOrDefaultAsync(id);
            if (log == null) throw new UserFriendlyException("Efor kaydı bulunamadı.");

            bool canDelete = ctx.EmployeeId.HasValue && log.EmployeeId == ctx.EmployeeId.Value; // kendi eforu
            if (!canDelete && IsAdmin(ctx.Role)) canDelete = true;                              // Admin tümü
            if (!canDelete && IsManager(ctx.Role) && log.ServiceRequestId.HasValue)             // TakımLideri: kendi takımı
            {
                var req = await _requestRepository.FirstOrDefaultAsync(log.ServiceRequestId.Value);
                canDelete = req != null && IsManagerForRequest(req, ctx);
            }
            if (!canDelete)
                throw new UserFriendlyException("Bu efor kaydını silme yetkiniz yok.");
            await _logRepository.DeleteAsync(id);
        }

        // --- Faz 2: Portaldan idempotent upsert ---

        public async Task<long> UpsertFromPortalAsync(PortalRequestDto input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Title))
                throw new UserFriendlyException("Geçersiz talep verisi (başlık zorunlu).");

            ServiceRequest entity = null;
            if (!string.IsNullOrWhiteSpace(input.ExternalRef))
            {
                var extRef = input.ExternalRef.Trim();
                entity = await _requestRepository.GetAll()
                    .FirstOrDefaultAsync(r => r.Source == input.Source && r.ExternalRef == extRef);
            }

            // Ham alanları çöz: e-posta → personel, grup → takım, durum/öncelik metni → enum.
            long? resolvedEmpId = await ResolveEmployeeByEmailAsync(input.AssigneeEmail);
            long? resolvedTeamId = resolvedEmpId.HasValue
                ? await _employeeRepository.GetAll().AsNoTracking().Where(e => e.Id == resolvedEmpId.Value).Select(e => e.TeamId).FirstOrDefaultAsync()
                : await ResolveTeamByNameAsync(input.GroupName);
            RequestStatus? mappedStatus = input.Status ?? MapStatusText(input.StatusText);
            int? mappedScore = input.PriorityScore ?? MapPriorityScore(input.PriorityText);

            bool isNew = entity == null;
            if (isNew)
            {
                entity = new ServiceRequest
                {
                    TenantId = AbpSession.TenantId ?? 1,
                    Source = input.Source,
                    ExternalRef = string.IsNullOrWhiteSpace(input.ExternalRef) ? null : input.ExternalRef.Trim(),
                    Status = mappedStatus ?? (resolvedEmpId.HasValue ? RequestStatus.Atandi : RequestStatus.Yeni),
                    CompletionPercentage = 0,
                    PriorityScore = ClampScore(mappedScore ?? 5),
                    Priority = ScoreToPriority(mappedScore ?? 5),
                    AssignedEmployeeId = resolvedEmpId,   // ilk içe aktarımda portal ataması uygulanır
                    TeamId = resolvedTeamId
                };
                await _requestRepository.InsertAsync(entity);
            }

            // Kaynak alanları her zaman güncelle (portal bunların sahibi).
            entity.ExternalUrl = input.ExternalUrl ?? entity.ExternalUrl;
            entity.Title = input.Title;
            entity.Description = input.Description ?? entity.Description;
            entity.RequesterName = input.RequesterName ?? entity.RequesterName;
            entity.RequesterEmail = input.RequesterEmail ?? entity.RequesterEmail;
            entity.ExtraInfo = input.ExtraInfo ?? entity.ExtraInfo;
            entity.ReceivedDate = input.ReceivedDate ?? entity.ReceivedDate ?? DateTime.Now;
            entity.DueDate = input.DueDate ?? entity.DueDate;
            if (mappedScore.HasValue) entity.PriorityScore = ClampScore(mappedScore.Value);
            if (!entity.TeamId.HasValue && resolvedTeamId.HasValue) entity.TeamId = resolvedTeamId;

            // Atama/durum YERELDE korunur (güncellemede portal ezmez). İstisna: portal kapanış/iptal bildirdiyse kapat.
            if (mappedStatus.HasValue &&
                (mappedStatus == RequestStatus.Kapandi || mappedStatus == RequestStatus.Iptal) &&
                entity.Status != RequestStatus.Kapandi && entity.Status != RequestStatus.Iptal)
            {
                entity.Status = mappedStatus.Value;
                entity.CompletionPercentage = mappedStatus == RequestStatus.Kapandi ? 100 : entity.CompletionPercentage;
                entity.ResolvedDate = input.ResolvedDate ?? entity.ResolvedDate ?? DateTime.Now;
                entity.ClosedDate = DateTime.Now;
            }

            await CurrentUnitOfWork.SaveChangesAsync();
            return entity.Id;
        }

        private async Task<long?> ResolveEmployeeByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var e = email.Trim();
            return await _employeeRepository.GetAll().AsNoTracking()
                .Where(x => x.Email != null && x.Email.ToLower() == e.ToLower())
                .Select(x => (long?)x.Id).FirstOrDefaultAsync();
        }

        private async Task<long?> ResolveTeamByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var n = name.Trim();
            return await _teamRepository.GetAll().AsNoTracking()
                .Where(t => t.Name == n)
                .Select(t => (long?)t.Id).FirstOrDefaultAsync();
        }

        // Portal durum metnini bizim RequestStatus'a eşler (TR/EN varyantları).
        private static RequestStatus? MapStatusText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim().ToLowerInvariant();
            if (t.Contains("iptal") || t.Contains("red") || t.Contains("cancel")) return RequestStatus.Iptal;
            if (t.Contains("kapat") || t.Contains("kapan") || t.Contains("tamamlan") || t.Contains("closed") || t.Contains("complete") || t.Contains("done")) return RequestStatus.Kapandi;
            if (t.Contains("çöz") || t.Contains("coz") || t.Contains("resolve")) return RequestStatus.Cozuldu;
            if (t.Contains("bekle") || t.Contains("pending") || t.Contains("hold")) return RequestStatus.Beklemede;
            if (t.Contains("kurulum") || t.Contains("işlem") || t.Contains("islem") || t.Contains("devam") || t.Contains("progress")) return RequestStatus.DevamEdiyor;
            if (t.Contains("atan") || t.Contains("assign")) return RequestStatus.Atandi;
            if (t.Contains("yeni") || t.Contains("açık") || t.Contains("acik") || t.Contains("open")) return RequestStatus.Yeni;
            return null;
        }

        // Portal öncelik metnini 1-10 skora eşler.
        private static int? MapPriorityScore(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim().ToLowerInvariant();
            if (t.Contains("krit") || t.Contains("acil") || t.Contains("critical") || t.Contains("urgent")) return 9;
            if (t.Contains("yüksek") || t.Contains("yuksek") || t.Contains("high")) return 7;
            if (t.Contains("düşük") || t.Contains("dusuk") || t.Contains("low")) return 3;
            if (t.Contains("orta") || t.Contains("normal") || t.Contains("medium")) return 5;
            return null;
        }

        private static TaskPriority ScoreToPriority(int score) =>
            score >= 9 ? TaskPriority.Kritik : score >= 7 ? TaskPriority.Yuksek : score >= 4 ? TaskPriority.Normal : TaskPriority.Dusuk;

        // --- yardımcılar ---

        private void EnsureCanManage(ServiceRequest r, (string Role, string Email, long? EmployeeId) ctx)
        {
            if (!IsManagerForRequest(r, ctx))
                throw new UserFriendlyException("Bu talep üzerinde yetkiniz yok.");
        }

        private static int ClampScore(int score) => score < 1 ? 1 : (score > 10 ? 10 : score);

        // Duruma göre ilerleme % (görevlerdeki mantıkla uyumlu).
        private static int ProgressForStatus(RequestStatus status, int currentPct)
        {
            int Clamp(int p) => p < 0 ? 0 : (p > 100 ? 100 : p);
            switch (status)
            {
                case RequestStatus.Kapandi:
                case RequestStatus.Cozuldu:
                    return 100;
                case RequestStatus.Yeni:
                case RequestStatus.Atandi:
                    return 0;
                case RequestStatus.DevamEdiyor:
                    return (currentPct <= 0 || currentPct >= 100) ? 25 : currentPct;
                default: // Beklemede, Iptal
                    return Clamp(currentPct);
            }
        }

        private async Task<long?> ResolveTeamIdAsync(long? assignedEmployeeId, long? fallbackEmployeeId)
        {
            var empId = assignedEmployeeId ?? fallbackEmployeeId;
            if (!empId.HasValue) return null;
            return await _employeeRepository.GetAll().AsNoTracking()
                .Where(e => e.Id == empId.Value).Select(e => e.TeamId).FirstOrDefaultAsync();
        }

        public static string SourceText(RequestSource s) =>
            s == RequestSource.SunucuKurulum ? "Sunucu Kurulum" : "Dış Destek";

        public static string StatusText(RequestStatus s)
        {
            switch (s)
            {
                case RequestStatus.Yeni: return "Yeni";
                case RequestStatus.Atandi: return "Atandı";
                case RequestStatus.DevamEdiyor: return "Devam Ediyor";
                case RequestStatus.Beklemede: return "Beklemede";
                case RequestStatus.Cozuldu: return "Çözüldü";
                case RequestStatus.Kapandi: return "Kapandı";
                case RequestStatus.Iptal: return "İptal";
                default: return s.ToString();
            }
        }

        private ServiceRequestDto MapRequest(ServiceRequest r, (string Role, string Email, long? EmployeeId) ctx)
        {
            var dto = ObjectMapper.Map<ServiceRequestDto>(r);
            dto.SourceText = SourceText(r.Source);
            dto.StatusText = StatusText(r.Status);
            dto.AssignedEmployeeName = r.AssignedEmployee?.FullName;
            dto.SecondaryEmployeeName = r.SecondaryEmployee?.FullName;
            dto.TeamName = r.Team?.Name;
            dto.CategoryName = r.Category?.Name ?? r.SubCategory?.Category?.Name;
            dto.SubCategoryName = r.SubCategory?.Name;
            dto.ProjectName = r.Project?.Name;
            dto.LogCount = r.Logs?.Count ?? 0;
            dto.TotalHours = r.Logs?.Sum(l => l.HoursSpent) ?? 0m;
            dto.IsOpen = r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal;
            dto.IsOverdue = dto.IsOpen && r.DueDate.HasValue && r.DueDate.Value < DateTime.Now;
            dto.CanManage = IsManagerForRequest(r, ctx);
            dto.CanLogEffort = r.AssignedEmployeeId.HasValue && ctx.EmployeeId.HasValue && r.AssignedEmployeeId == ctx.EmployeeId;
            return dto;
        }

        private ActivityLogDto MapLog(ActivityLog a)
        {
            if (a == null) return null;
            var dto = ObjectMapper.Map<ActivityLogDto>(a);
            dto.EmployeeName = a.Employee?.FullName;
            dto.ServiceRequestTitle = a.ServiceRequest?.Title;
            return dto;
        }
    }
}
