using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private readonly IRepository<IntegrationSource, int> _sourceRepository;
        private readonly ActivityManagement.Notifications.INotificationManager _notificationManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;

        public ServiceRequestAppService(
            IRepository<ServiceRequest, long> requestRepository,
            IRepository<ActivityLog, long> logRepository,
            IRepository<Employee, long> employeeRepository,
            IRepository<Team, long> teamRepository,
            IRepository<IntegrationSource, int> sourceRepository,
            ActivityManagement.Notifications.INotificationManager notificationManager,
            IHttpContextAccessor httpContextAccessor,
            System.Net.Http.IHttpClientFactory httpClientFactory)
        {
            _requestRepository = requestRepository;
            _logRepository = logRepository;
            _employeeRepository = employeeRepository;
            _teamRepository = teamRepository;
            _sourceRepository = sourceRepository;
            _notificationManager = notificationManager;
            _httpContextAccessor = httpContextAccessor;
            _httpClientFactory = httpClientFactory;
            // Ortak base yetki yardımcıları için (property-injection'a bağımlı kalmadan garanti atama).
            AuthHttpContextAccessor = httpContextAccessor;
            AuthEmployeeRepository = employeeRepository;
        }

        // --- bağlam / yetki yardımcıları: CurrentContext / IsManager / IsCrossTeamManager / IsAdminSelfContext /
        //     SeesAllTeams / EffectiveRole / CurrentEmployeeAppRole / CurrentEmployeeTeamId → ortak base sınıfa taşındı
        //     (ActivityManagementAppServiceBase). Davranış birebir aynı. ---

        // (B10) Görünürlük kapsamı: Admin/Manager tümü; TakımLideri kendi TAKIMI + kendine atanan; Uzman yalnız kendine atanan.
        // ATANMAMIŞ + PSM (SunucuKurulum) talepleri HERKESE görünür — PSM'den atansız gelen talep aksi halde
        // kimseye görünmeyip kaybolur (triyaj için "Atanmamış Talepler" sekmesi). Destek'te bu KURAL YOK
        // (destek atansız yığını herkese açılmaz; destek kendi kapsamı + kişi filtresiyle yönetilir).
        private IQueryable<ServiceRequest> ApplyVisibilityScope(IQueryable<ServiceRequest> q)
        {
            if (SeesAllTeams()) return q;
            long? empId = long.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("EmployeeId")?.Value, out var e) ? e : (long?)null;
            // Yalnız PSM (SunucuKurulum) + atanmamış talepler kapsam dışı herkese görünür.
            if (!empId.HasValue)
                return q.Where(r => r.AssignedEmployeeId == null && r.Source == RequestSource.SunucuKurulum);
            if (string.Equals(EffectiveRole(), "TakımLideri", StringComparison.OrdinalIgnoreCase))
            {
                var myTeam = CurrentEmployeeTeamId(empId);
                return q.Where(r => (r.AssignedEmployeeId == null && r.Source == RequestSource.SunucuKurulum)
                                    || r.AssignedEmployeeId == empId.Value || r.SecondaryEmployeeId == empId.Value
                                    || (myTeam != null && r.TeamId == myTeam));
            }
            return q.Where(r => (r.AssignedEmployeeId == null && r.Source == RequestSource.SunucuKurulum)
                                || r.AssignedEmployeeId == empId.Value || r.SecondaryEmployeeId == empId.Value);
        }

        // Yönetici bu talebi yönetebilir mi: Admin her zaman; TakımLideri yalnız kendi takımının (takımsız dahil).
        private bool IsManagerForRequest(ServiceRequest r, (string Role, string Email, long? EmployeeId) ctx)
        {
            if (!IsManager(ctx.Role)) return false;
            if (IsCrossTeamManager(ctx.Role)) return true; // Admin/Manager → tüm takımlar
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
                .WhereIf(input.OnlyNoEffort == true, r => !r.Logs.Any())
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), r =>
                    r.Title.Contains(input.Filter) ||
                    (r.ExternalRef != null && r.ExternalRef.Contains(input.Filter)) ||
                    (r.RequesterName != null && r.RequesterName.Contains(input.Filter)));

            // Yalnız bana atanan
            if (input.MineOnly == true && ctx.EmployeeId.HasValue)
                query = query.Where(r => r.AssignedEmployeeId == ctx.EmployeeId.Value ||
                                         r.SecondaryEmployeeId == ctx.EmployeeId.Value);

            // Görünürlük (B10): Admin/Manager tümü; TakımLideri kendi takımı + kendine; Uzman yalnız kendine.
            if (input.MineOnly != true)
                query = ApplyVisibilityScope(query);

            // Açık talepler önce; sonra önem skoru, sonra yakın SLA. (A2) MaxResultCount server-side uygulanır.
            var items = await query
                .OrderByDescending(r => r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal)
                .ThenByDescending(r => r.PriorityScore)
                .ThenBy(r => r.DueDate ?? DateTime.MaxValue)
                .Take(Math.Clamp(input.MaxResultCount, 1, 100000))
                .ToListAsync();
            return items.Select(r => MapRequest(r, ctx)).ToList();
        }

        public async Task<ServiceRequestDto> GetAsync(long id)
        {
            var ctx = CurrentContext();
            // GÜVENLİK (IDOR): detay da listelerle AYNI görünürlük kapsamına tabi — kapsam dışıysa kayıt "yok" sayılır.
            var r = await WithIncludes(ApplyVisibilityScope(_requestRepository.GetAll().AsNoTracking()))
                .Include(x => x.Comments)      // detayda portal yorumları + dosya ekleri (listelerde yüklenmez)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (r == null) throw new UserFriendlyException("Talep bulunamadı veya erişim yetkiniz yok.");
            var dto = MapRequest(r, ctx);
            // (C13) Portal talebinde durum/yorum write-back UI'si için kaynağın write-back durumu
            if (dto.IsPortal)
                dto.SourceWriteBackEnabled = await _sourceRepository.GetAll().AsNoTracking()
                    .AnyAsync(s => s.Source == r.Source && s.WriteBackEnabled);
            return dto;
        }

        // (A3) Talepler ana ekranı — VERİMLİ: sekme başına SINIRLI (cap) liste + gerçek SQL sayaçları.
        // Tüm talepleri + Logs'u belleğe yüklemez. Arşiv/aktif ayrımı SQL'de.
        public async Task<ServiceRequestsIndexDto> GetIndexAsync(int cap = 500)
        {
            var ctx = CurrentContext();
            if (cap < 1) cap = 1; else if (cap > 2000) cap = 2000;

            // Görünürlük kapsamı (B10): Admin/Manager tümü; TakımLideri kendi takımı; Uzman yalnız kendine
            IQueryable<ServiceRequest> Scoped() => ApplyVisibilityScope(_requestRepository.GetAll().AsNoTracking());
            // (Y3) ARŞİV = yalnız KAPANDI + efor girilmiş. Çözüldü KAPATILAN sayılmaz → AKTİF'te kalır
            // (kapatıldı/kapandı yapılınca arşive düşer). AKTİF = arşiv değil ve İptal değil.
            IQueryable<ServiceRequest> Active(RequestSource src) => Scoped()
                .Where(r => r.Source == src && r.Status != RequestStatus.Iptal
                            && !(r.Status == RequestStatus.Kapandi && r.Logs.Any()));
            IQueryable<ServiceRequest> Archived() => Scoped()
                .Where(r => r.Status == RequestStatus.Kapandi && r.Logs.Any());
            // ATANMAMIŞ (triyaj): YALNIZ PSM (SunucuKurulum) + sorumlusu olmayan + açık talepler. Herkese görünür.
            IQueryable<ServiceRequest> Unassigned() => Scoped()
                .Where(r => r.AssignedEmployeeId == null && r.Source == RequestSource.SunucuKurulum
                            && r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal);

            async Task<System.Collections.Generic.List<ServiceRequestDto>> Materialize(IQueryable<ServiceRequest> orderedIdQuery)
            {
                var ids = await orderedIdQuery.Select(r => r.Id).Take(cap).ToListAsync();
                if (ids.Count == 0) return new System.Collections.Generic.List<ServiceRequestDto>();
                var rows = await WithIncludes(_requestRepository.GetAll().AsNoTracking()).Where(r => ids.Contains(r.Id)).ToListAsync();
                var map = rows.ToDictionary(r => r.Id);
                return ids.Where(map.ContainsKey).Select(id => MapRequest(map[id], ctx)).ToList();
            }

            var dto = new ServiceRequestsIndexDto { Cap = cap };
            dto.CountSunucu = await Active(RequestSource.SunucuKurulum).CountAsync();
            dto.CountDestek = await Active(RequestSource.DisDestek).CountAsync();
            dto.CountUnassigned = await Unassigned().CountAsync();
            dto.CountArchived = await Archived().CountAsync();

            // Aktif: açık talepler önce, sonra önem, sonra SLA. Arşiv: en son çözülen/kapanan önce.
            dto.ActiveSunucu = await Materialize(Active(RequestSource.SunucuKurulum)
                .OrderByDescending(r => r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal && r.Status != RequestStatus.Cozuldu)
                .ThenByDescending(r => r.PriorityScore).ThenBy(r => r.DueDate ?? DateTime.MaxValue));
            dto.ActiveDestek = await Materialize(Active(RequestSource.DisDestek)
                .OrderByDescending(r => r.Status != RequestStatus.Kapandi && r.Status != RequestStatus.Iptal && r.Status != RequestStatus.Cozuldu)
                .ThenByDescending(r => r.PriorityScore).ThenBy(r => r.DueDate ?? DateTime.MaxValue));
            // Atanmamış: en yeni gelen önce, sonra önem — triyaj için.
            dto.Unassigned = await Materialize(Unassigned()
                .OrderByDescending(r => r.PriorityScore).ThenByDescending(r => r.CreationTime));
            dto.Archived = await Materialize(Archived()
                .OrderByDescending(r => r.ResolvedDate ?? r.ClosedDate ?? r.DueDate));
            return dto;
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
                // Faaliyet tipi: verilmezse Sunucu Kurulum talepleri varsayılan "Kurulum" (sonra değiştirilebilir)
                ActivityType = !string.IsNullOrWhiteSpace(input.ActivityType) ? input.ActivityType
                               : (input.Source == RequestSource.SunucuKurulum ? "Kurulum" : null),
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

            if (entity.AssignedEmployeeId.HasValue)
                await _notificationManager.NotifyAsync(entity.AssignedEmployeeId, NotificationType.TalepAtandi,
                    "Size bir talep atandı", entity.Title, $"/Requests/Detail/{entity.Id}", severity: "info", actorEmployeeId: ctx.EmployeeId);

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
            r.ActivityType = input.ActivityType;
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

            var previousAssignee = r.AssignedEmployeeId;
            r.AssignedEmployeeId = assignedEmployeeId;
            r.SecondaryEmployeeId = secondaryEmployeeId;
            r.TeamId = await ResolveTeamIdAsync(assignedEmployeeId, ctx.EmployeeId) ?? r.TeamId;
            if (assignedEmployeeId.HasValue && r.Status == RequestStatus.Yeni)
                r.Status = RequestStatus.Atandi;
            await CurrentUnitOfWork.SaveChangesAsync();

            // Bildirim: yeni bir kişiye atandıysa (kendine değilse)
            if (assignedEmployeeId.HasValue && assignedEmployeeId != previousAssignee)
                await _notificationManager.NotifyAsync(assignedEmployeeId, NotificationType.TalepAtandi,
                    "Size bir talep atandı", r.Title, $"/Requests/Detail/{r.Id}", severity: "info", actorEmployeeId: ctx.EmployeeId);

            return await GetAsync(r.Id);
        }

        // TOPLU ATAMA: seçili taleplerin sorumlusunu topluca belirler. Yetkisi olmayan talep ATLANIR.
        // Özellikle atanmamış (PSM/destek) yığınının triyajı için. Dönüş: gerçekten atanan adet.
        public async Task<int> BulkAssignAsync(List<long> ids, long? assignedEmployeeId, long? secondaryEmployeeId = null)
        {
            var ctx = CurrentContext();
            if (ids == null || ids.Count == 0) return 0;
            var reqs = await _requestRepository.GetAll().Where(r => ids.Contains(r.Id)).ToListAsync();
            int done = 0;
            foreach (var r in reqs)
            {
                if (!IsManagerForRequest(r, ctx)) continue; // yetki dışını sessizce atla
                var prev = r.AssignedEmployeeId;
                r.AssignedEmployeeId = assignedEmployeeId;
                r.SecondaryEmployeeId = secondaryEmployeeId;
                r.TeamId = await ResolveTeamIdAsync(assignedEmployeeId, ctx.EmployeeId) ?? r.TeamId;
                if (assignedEmployeeId.HasValue && r.Status == RequestStatus.Yeni)
                    r.Status = RequestStatus.Atandi;
                if (assignedEmployeeId.HasValue && assignedEmployeeId != prev)
                    await _notificationManager.NotifyAsync(assignedEmployeeId, NotificationType.TalepAtandi,
                        "Size bir talep atandı", r.Title, $"/Requests/Detail/{r.Id}", severity: "info", actorEmployeeId: ctx.EmployeeId);
                done++;
            }
            await CurrentUnitOfWork.SaveChangesAsync();
            return done;
        }

        public async Task<ServiceRequestDto> UpdateStatusAsync(long id, RequestStatus status, int percentage, string note = null)
        {
            var ctx = CurrentContext();
            var r = await _requestRepository.GetAsync(id);

            bool isPortal = !string.IsNullOrWhiteSpace(r.ExternalRef);
            bool writeBack = false;
            if (isPortal)
            {
                // Portal talebi: yalnız write-back AÇIK kaynakta yerelden değiştirilebilir (komut → portala POST → sonraki
                // senkronda teyit). Kapalıysa durum yalnız portaldan çekilir (bizde efor).
                writeBack = await _sourceRepository.GetAll().AsNoTracking().AnyAsync(s => s.Source == r.Source && s.WriteBackEnabled);
                if (!writeBack)
                    throw new UserFriendlyException("Bu talebin durumu destek portalından güncellenir. Burada yalnızca efor girebilirsiniz.");
            }

            bool canManage = IsManagerForRequest(r, ctx);
            bool isAssignee = ctx.EmployeeId.HasValue &&
                (r.AssignedEmployeeId == ctx.EmployeeId || r.SecondaryEmployeeId == ctx.EmployeeId); // 1. VEYA 2. sorumlu (UI ile uyumlu)
            if (!canManage && !isAssignee)
                throw new UserFriendlyException("Bu talebin durumunu güncelleme yetkiniz yok.");

            // Portal + write-back: ÖNCE portala POST (komut deseni). Portal reddederse yerelde DEĞİŞTİRME
            // (portal tek doğruluk kaynağı; başarısız komutu yerelde uygulayıp sonraki senkronda geri almaktansa hiç uygulama).
            if (isPortal && writeBack)
                await PushStatusToPortalAsync(r, status, note);

            r.Status = status;
            r.CompletionPercentage = ProgressForStatus(status, percentage > 0 ? percentage : r.CompletionPercentage);
            r.ResolvedDate = status == RequestStatus.Cozuldu ? (r.ResolvedDate ?? DateTime.Now) : r.ResolvedDate;
            r.ClosedDate = status == RequestStatus.Kapandi ? DateTime.Now : (status == RequestStatus.Iptal ? DateTime.Now : null);
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(r.Id);
        }

        // (Durum) Destek talebinde durumu DESTEK'İN kendi 9'lu listesiyle günceller. statusCode (open/in_progress/...)
        // portala POST edilir; yerelde bizim RequestStatus'a eşlenir + ham etiket (PortalStatusText) saklanır.
        // Yalnız portal + write-back açık; yetki: yönetici veya atanan/2. sorumlu. Portal reddederse yerelde değişmez.
        public async Task<ServiceRequestDto> UpdatePortalStatusAsync(long id, string statusCode, string note = null)
        {
            var ctx = CurrentContext();
            var r = await _requestRepository.GetAsync(id);
            if (string.IsNullOrWhiteSpace(r.ExternalRef))
                throw new UserFriendlyException("Bu işlem yalnızca portal talepleri içindir.");
            if (!PortalStatusCatalog.IsValid(statusCode))
                throw new UserFriendlyException("Geçersiz durum seçildi.");

            var writeBack = await _sourceRepository.GetAll().AsNoTracking().AnyAsync(s => s.Source == r.Source && s.WriteBackEnabled);
            if (!writeBack)
                throw new UserFriendlyException("Bu kaynak için portala durum güncelleme (write-back) etkin değil.");

            bool canManage = IsManagerForRequest(r, ctx);
            bool isAssignee = ctx.EmployeeId.HasValue && (r.AssignedEmployeeId == ctx.EmployeeId || r.SecondaryEmployeeId == ctx.EmployeeId);
            if (!canManage && !isAssignee)
                throw new UserFriendlyException("Bu talebin durumunu güncelleme yetkiniz yok.");

            // ÖNCE portala POST (komut deseni); portal reddederse istisna → yerelde değişmez.
            await PushStatusRawToPortalAsync(r, statusCode, note);

            var mapped = MapStatusText(statusCode) ?? r.Status;
            r.Status = mapped;
            r.PortalStatusText = PortalStatusCatalog.LabelOf(statusCode);
            r.CompletionPercentage = ProgressForStatus(mapped, r.CompletionPercentage);
            if (mapped == RequestStatus.Cozuldu) { r.ResolvedDate = r.ResolvedDate ?? DateTime.Now; r.ClosedDate = null; }
            else if (mapped == RequestStatus.Kapandi) { r.ResolvedDate = r.ResolvedDate ?? DateTime.Now; r.ClosedDate = r.ClosedDate ?? DateTime.Now; }
            else if (mapped == RequestStatus.Iptal) { r.ClosedDate = r.ClosedDate ?? DateTime.Now; }
            else { r.ResolvedDate = null; r.ClosedDate = null; }
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(r.Id);
        }

        // (C13) Yerelde talebe yorum ekler.
        //  - DIŞ not (isInternal=false): write-back AÇIK olmalı → portala POST → müşteriye e-posta.
        //  - İÇ not (isInternal=true): write-back AÇIK ise portala dahili not olarak POST edilir; KAPALI ise
        //    YALNIZ YEREL saklanır (bizim özel notumuz). Dönen id ile yerel ayna (sonraki senkronda kopyalanmaz).
        // Yalnız portal talebinde; yetki: yönetici veya atanan/2. sorumlu.
        public async Task AddCommentAsync(long id, string body, bool isInternal, List<CommentUploadFile> files = null)
        {
            var ctx = CurrentContext();
            var r = await _requestRepository.GetAll().Include(x => x.Comments).Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (r == null) throw new UserFriendlyException("Talep bulunamadı.");
            if (string.IsNullOrWhiteSpace(r.ExternalRef))
                throw new UserFriendlyException("Yorum gönderimi yalnızca portal talepleri için geçerlidir.");

            bool canManage = IsManagerForRequest(r, ctx);
            bool isAssignee = ctx.EmployeeId.HasValue && (r.AssignedEmployeeId == ctx.EmployeeId || r.SecondaryEmployeeId == ctx.EmployeeId);
            if (!canManage && !isAssignee)
                throw new UserFriendlyException("Bu talebe yorum ekleme yetkiniz yok.");

            var writeBack = await _sourceRepository.GetAll().AsNoTracking().AnyAsync(s => s.Source == r.Source && s.WriteBackEnabled);

            files ??= new List<CommentUploadFile>();
            string newId = null;
            var returnedAtts = new List<PortalAttachmentDto>();
            string localBody = body;
            bool pushedOk = false;

            if (string.IsNullOrWhiteSpace(StripTags(body)) && files.Count == 0)
                throw new UserFriendlyException("Yorum veya dosya girmelisiniz.");

            if (writeBack)
            {
                // Ctrl+V ile gömülen base64 görselleri DOSYA'ya çıkar; gövde metne indirger (destek dosyaları ayrı alır).
                var portalBody = ExtractInlineImages(body, files);
                try
                {
                    (newId, returnedAtts) = await PushCommentToPortalAsync(r, portalBody?.Trim(), isInternal, files);
                    localBody = portalBody; // portal aldı → görseller "Portal Dosya Ekleri"nde, gövde metin
                    pushedOk = true;
                }
                catch (Exception ex) when (isInternal)
                {
                    // (Y1) İÇ NOT: portal reddetse de (ör. destek multipart ucu bozuk) not KAYBOLMASIN → YEREL sakla.
                    // Orijinal gövde korunur (base64 görsel inline → bizde görünür); portala gitmez, dış kullanıcıya ulaşmaz.
                    Logger.Warn($"İç not portala iletilemedi, yerelde saklanıyor (Source={r.Source}, Ref={r.ExternalRef}): {ex.Message}");
                    localBody = body; newId = null; returnedAtts = new List<PortalAttachmentDto>();
                }
                // DIŞ not'ta istisna yakalanmaz → yukarı fırlar (müşteriye ulaşması gereken not sessizce yutulmaz).
            }
            else
            {
                // Write-back kapalı: DIŞ not gönderilemez; İÇ not yalnız yerelde saklanır (base64 görsel gövdede kalır → yerelde görünür).
                if (!isInternal)
                    throw new UserFriendlyException("Dış not (müşteriye açık) göndermek için bu kaynakta write-back açık olmalı (Admin → Entegrasyon). İç not olarak kaydedebilirsiniz.");
            }

            // (B4) Yerel yorum aynası. Portala BAŞARIYLA gitti ama id DÖNMEDİYSE yerel kopya EKLENMEZ:
            // aksi halde sonraki detay senkronu portalın gerçek id'siyle aynı yorumu getirir ve
            // (null id ≠ gerçek id) dedup tutmaz → yorum İKİ KEZ görünürdü. Bu durumda senkron getirir.
            if (pushedOk && string.IsNullOrWhiteSpace(newId))
            {
                Logger.Warn($"Portal yorum id döndürmedi; yerel kopya atlandı, senkron getirecek (Source={r.Source}, Ref={r.ExternalRef}).");
            }
            else
            {
                r.Comments.Add(new ServiceRequestComment
                {
                    TenantId = r.TenantId, ServiceRequestId = r.Id,
                    ExternalCommentId = string.IsNullOrWhiteSpace(newId) ? null : newId,
                    AuthorName = Trunc(ctx.Email, 256), AuthorEmail = Trunc(ctx.Email, 256),
                    CommentDate = DateTime.Now, Body = localBody?.Trim(), IsInternal = isInternal
                });
            }

            // Portalın döndürdüğü ekleri yerel aynaya (dedup ExternalAttachmentId) → "Portal Dosya Ekleri"nde görünür + indirilebilir
            if (returnedAtts.Count > 0)
            {
                var seen = new System.Collections.Generic.HashSet<string>(
                    r.Attachments.Where(a => a.ExternalAttachmentId != null).Select(a => a.ExternalAttachmentId),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var a in returnedAtts)
                {
                    if (string.IsNullOrWhiteSpace(a.Id) || !seen.Add(a.Id)) continue;
                    r.Attachments.Add(new ServiceRequestAttachment
                    {
                        TenantId = r.TenantId, ServiceRequestId = r.Id, ExternalAttachmentId = a.Id,
                        FileName = Trunc(a.Name, 512), Url = Trunc(a.Url, 1024), SizeBytes = a.SizeBytes,
                        ContentType = Trunc(a.ContentType, 256), UploadedAt = a.UploadedAt
                    });
                }
            }
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        // --- (C13) portal write-back istemcisi (giden POST) ---

        // Bizim durum → destek durum KODU (kullanıcı listesi: open/in_progress/pending/resolved/rejected/closed).
        // Bizim model daha kaba: Atandı→in_progress; Beklemede→pending (transferred_to_dev/waiting_* ayrımı bizde yok).
        private static string StatusToPortalText(RequestStatus s)
        {
            switch (s)
            {
                case RequestStatus.Yeni: return "open";
                case RequestStatus.Atandi: return "in_progress";
                case RequestStatus.DevamEdiyor: return "in_progress";
                case RequestStatus.Beklemede: return "pending";
                case RequestStatus.Cozuldu: return "resolved";
                case RequestStatus.Kapandi: return "closed";
                case RequestStatus.Iptal: return "rejected";
                default: return "in_progress";
            }
        }

        private async Task<(string baseUrl, string apiKey, string authHeader, string authScheme, string userEmail, bool writeBack)>
            GetSourceAuthAsync(RequestSource source)
        {
            var src = await _sourceRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(s => s.Source == source);
            if (src == null) return (null, null, "Authorization", "", null, false);
            return (src.BaseUrl,
                    ActivityManagement.Security.DpapiProtector.Unprotect(src.ApiKey),
                    string.IsNullOrWhiteSpace(src.AuthHeader) ? "Authorization" : src.AuthHeader,
                    src.AuthScheme ?? "",
                    src.UserEmail,
                    src.WriteBackEnabled);
        }

        private void ApplyAuth(HttpRequestMessage req, string apiKey, string authHeader, string authScheme, string userEmail, string actorEmail)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var val = string.IsNullOrWhiteSpace(authScheme) ? apiKey : authScheme.Trim() + " " + apiKey;
                req.Headers.TryAddWithoutValidation(authHeader, val);
            }
            // X-User-Email: işlemi yapan aktör (varsa oturumdaki kullanıcı, yoksa kaynağın servis e-postası)
            var actor = !string.IsNullOrWhiteSpace(actorEmail) ? actorEmail : userEmail;
            if (!string.IsNullOrWhiteSpace(actor))
                req.Headers.TryAddWithoutValidation("X-User-Email", actor.Trim());
        }

        private static string PortalBasePath(string baseUrl)
            => baseUrl.Contains("?") ? baseUrl.Substring(0, baseUrl.IndexOf('?')) : baseUrl;

        // Bilinen portal dosya backend host'ları (destek/Cortex ekleri bu Azure backend'inden servis edilir).
        // Not: ileride kaynak başına yapılandırılabilir alana taşınabilir (şimdilik sabit allow-list).
        private static readonly System.Collections.Generic.HashSet<string> KnownPortalFileHosts =
            new(StringComparer.OrdinalIgnoreCase) { "cortixsuite.azurewebsites.net" };

        // Portal dosya URL'si güvenli mi: https + host allow-list (BaseUrl host + bilinen backend) + özel-IP reddi.
        private static bool IsAllowedPortalFileUrl(string baseUrl, string fileUrl, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(fileUrl) || !Uri.TryCreate(fileUrl, UriKind.Absolute, out var u)) return false;
            if (!string.Equals(u.Scheme, "https", StringComparison.OrdinalIgnoreCase)) return false;

            var allowed = new System.Collections.Generic.HashSet<string>(KnownPortalFileHosts, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var bu))
                allowed.Add(bu.Host);
            if (!allowed.Contains(u.Host)) return false;

            if (System.Net.IPAddress.TryParse(u.Host, out var ip) && IsPrivateOrLoopback(ip)) return false;
            uri = u;
            return true;
        }

        private static bool IsPrivateOrLoopback(System.Net.IPAddress ip)
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return true;
            var b = ip.GetAddressBytes();
            if (b.Length == 4)
            {
                if (b[0] == 10) return true;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                if (b[0] == 192 && b[1] == 168) return true;
                if (b[0] == 169 && b[1] == 254) return true;   // link-local / cloud metadata
                if (b[0] == 127) return true;
            }
            return false;
        }

        private Task PushStatusToPortalAsync(ServiceRequest r, RequestStatus status, string note)
            => PushStatusRawToPortalAsync(r, StatusToPortalText(status), note);

        // Portala HAM durum değeri (destek kodu, ör. "waiting_for_customer") gönderir.
        private async Task PushStatusRawToPortalAsync(ServiceRequest r, string statusValue, string note)
        {
            var (baseUrl, apiKey, authHeader, authScheme, userEmail, writeBack) = await GetSourceAuthAsync(r.Source);
            if (!writeBack || string.IsNullOrWhiteSpace(baseUrl))
                throw new UserFriendlyException("Bu kaynak için portala yazma (write-back) etkin değil.");
            var url = PortalBasePath(baseUrl).TrimEnd('/') + "/" + Uri.EscapeDataString(r.ExternalRef) + "/durum";
            var payload = JsonSerializer.Serialize(new { status = statusValue, note = note });
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            ApplyAuth(req, apiKey, authHeader, authScheme, userEmail, CurrentContext().Email);
            var client = _httpClientFactory.CreateClient("PortalSync");
            client.Timeout = TimeSpan.FromSeconds(30);
            using var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                throw new UserFriendlyException($"Portal durum güncellemesini reddetti (HTTP {(int)resp.StatusCode}). Durum yerelde de değiştirilmedi.");
        }

        // (V3) Yorum + opsiyonel DOSYALAR portala. Dosya varsa multipart/form-data (destek Seçenek A), yoksa JSON.
        // Dönüş: (oluşan yorum id'si, portalın döndürdüğü ekler [id/url/...]).
        private async Task<(string commentId, List<PortalAttachmentDto> attachments)> PushCommentToPortalAsync(
            ServiceRequest r, string body, bool isInternal, List<CommentUploadFile> files)
        {
            var (baseUrl, apiKey, authHeader, authScheme, userEmail, writeBack) = await GetSourceAuthAsync(r.Source);
            if (!writeBack || string.IsNullOrWhiteSpace(baseUrl))
                throw new UserFriendlyException("Bu kaynak için portala yazma (write-back) etkin değil.");
            var url = PortalBasePath(baseUrl).TrimEnd('/') + "/" + Uri.EscapeDataString(r.ExternalRef) + "/yorumlar";

            bool hasFiles = files != null && files.Exists(f => f?.Content != null && f.Content.Length > 0);
            HttpContent content;
            if (hasFiles)
            {
                var mp = new MultipartFormDataContent();
                mp.Add(new StringContent(body ?? "", Encoding.UTF8), "body");
                mp.Add(new StringContent(isInternal ? "true" : "false"), "isInternal");
                foreach (var f in files)
                {
                    if (f?.Content == null || f.Content.Length == 0) continue;
                    var fc = new ByteArrayContent(f.Content);
                    if (!string.IsNullOrWhiteSpace(f.ContentType))
                        try { fc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(f.ContentType); } catch { }
                    mp.Add(fc, "files", string.IsNullOrWhiteSpace(f.FileName) ? "dosya" : f.FileName);
                }
                content = mp;
            }
            else
            {
                content = new StringContent(JsonSerializer.Serialize(new { body, isInternal }), Encoding.UTF8, "application/json");
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            ApplyAuth(req, apiKey, authHeader, authScheme, userEmail, CurrentContext().Email);
            var client = _httpClientFactory.CreateClient("PortalSync");
            client.Timeout = TimeSpan.FromSeconds(hasFiles ? 120 : 30);
            using var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var code = (int)resp.StatusCode;
                var msg = code == 413 ? "Dosya çok büyük (portal limiti 25 MB)."
                        : code == 415 ? "Desteklenmeyen dosya türü."
                        : $"Portal yorumu reddetti (HTTP {code}).";
                throw new UserFriendlyException(msg);
            }
            var respJson = await resp.Content.ReadAsStringAsync();
            string cid = null; var atts = new List<PortalAttachmentDto>();
            try
            {
                var doc = JsonSerializer.Deserialize<CommentPostResponse>(respJson, JsonOptsIgnoreCase);
                cid = doc?.Id;
                if (doc?.Attachments != null) atts = doc.Attachments;
            }
            catch { }
            return (cid, atts);
        }

        private class CommentPostResponse
        {
            public string Id { get; set; }
            public string ExternalRef { get; set; }
            public DateTime? CreatedAt { get; set; }
            public List<PortalAttachmentDto> Attachments { get; set; }
        }

        // Quill'in Ctrl+V ile gömdüğü base64 görselleri DOSYA'ya çıkarır ve gövdeden temizler (destek dosya olarak alır).
        private static readonly Regex InlineImgRx = new Regex(
            @"<img\b[^>]*?src\s*=\s*[""']data:(image/[a-z0-9.+-]+);base64,([A-Za-z0-9+/=\s]+)[""'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static string ExtractInlineImages(string html, List<CommentUploadFile> files)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;
            int n = 0;
            return InlineImgRx.Replace(html, m =>
            {
                try
                {
                    var mime = m.Groups[1].Value.Trim().ToLowerInvariant();
                    var b64 = Regex.Replace(m.Groups[2].Value, @"\s", "");
                    var bytes = Convert.FromBase64String(b64);
                    var ext = mime.Contains("png") ? "png" : (mime.Contains("jpe") || mime.Contains("jpg")) ? "jpg"
                            : mime.Contains("gif") ? "gif" : mime.Contains("webp") ? "webp" : "img";
                    files.Add(new CommentUploadFile { Content = bytes, ContentType = mime, FileName = $"ekran-goruntusu-{++n}.{ext}" });
                }
                catch { }
                return "";
            });
        }

        private static string StripTags(string html)
            => string.IsNullOrEmpty(html) ? "" : Regex.Replace(html, "<[^>]+>", "").Replace("&nbsp;", " ").Trim();

        private static readonly JsonSerializerOptions JsonOptsIgnoreCase = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // --- efor ---

        public async Task<ActivityLogDto> LogEffortAsync(CreateActivityLogDto input)
        {
            var ctx = CurrentContext();
            if (!input.ServiceRequestId.HasValue)
                throw new UserFriendlyException("Efor girişi için talep gereklidir.");

            var r = await _requestRepository.GetAll().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == input.ServiceRequestId.Value);
            if (r == null) throw new UserFriendlyException("Talep bulunamadı.");

            // Efor: atanan/2. sorumlu VEYA kapsamındaki yönetici (Admin/Manager/TakımLideri) kendi adına girer.
            bool canLog = IsManagerForRequest(r, ctx)
                || (ctx.EmployeeId.HasValue && (r.AssignedEmployeeId == ctx.EmployeeId || r.SecondaryEmployeeId == ctx.EmployeeId));
            if (!canLog)
                throw new UserFriendlyException("Bu talebe efor girme yetkiniz yok.");
            if (!ctx.EmployeeId.HasValue)
                throw new UserFriendlyException("Efor girmek için personel kaydınız bulunmuyor.");
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
                // Efor talebin tipini devralır (raporlama). Form tip gönderdiyse o, yoksa talebin tipi, o da yoksa "Talep".
                HoursSpent = input.HoursSpent,
                ActivityType = !string.IsNullOrWhiteSpace(input.ActivityType) ? input.ActivityType
                               : (!string.IsNullOrWhiteSpace(r.ActivityType) ? r.ActivityType : "Talep")
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
            // GÜVENLİK (IDOR): talebi göremeyen kullanıcı eforlarını da göremez.
            var canSee = await ApplyVisibilityScope(_requestRepository.GetAll().AsNoTracking())
                .AnyAsync(x => x.Id == serviceRequestId);
            if (!canSee) throw new UserFriendlyException("Talep bulunamadı veya erişim yetkiniz yok.");

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
            if (!canDelete && IsCrossTeamManager(ctx.Role)) canDelete = true;                              // Admin tümü
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
            // GÜVENLİK (defense-in-depth): bu metot yalnız portal entegrasyonu içindir —
            // anonim webhook (IntegrationController, token'lı) VEYA context'siz HostedService çağırır.
            // Dynamic API üzerinden kimliği doğrulanmış (ör. Uzman) bir kullanıcı çağıramaz; yalnız Admin istisna.
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true &&
                !string.Equals(user.FindFirst(ClaimTypes.Role)?.Value, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UserFriendlyException("Bu işlem yalnızca portal entegrasyonu üzerinden yapılır.");

            if (input == null || string.IsNullOrWhiteSpace(input.Title))
                throw new UserFriendlyException("Geçersiz talep verisi (başlık zorunlu).");

            // Portal alanlarını kolon sınırlarına kırp (aksi halde "String or binary data would be truncated"
            // tüm senkron partisini düşürür). Description nvarchar(max) → kırpılmaz.
            input.Title = Trunc(input.Title, 512);
            input.ExternalRef = Trunc(input.ExternalRef, 128);
            input.ExternalUrl = Trunc(input.ExternalUrl, 1024);
            input.RequesterName = Trunc(input.RequesterName, 256);
            input.RequesterEmail = Trunc(input.RequesterEmail, 256);
            input.ExtraInfo = Trunc(input.ExtraInfo, 2000);
            input.ActivityType = Trunc(input.ActivityType, 64);

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
            // A4: portalın gönderdiği durum metni eşlenemezse SESSİZCE kaybolmasın — logla ki MapStatusText genişletilsin.
            if (!mappedStatus.HasValue && !string.IsNullOrWhiteSpace(input.StatusText))
                Logger.Warn($"Eşlenemeyen talep durumu metni: '{input.StatusText}' (Source={input.Source}, Ref={input.ExternalRef}) — MapStatusText'e eklenmeli.");
            int? mappedScore = input.PriorityScore ?? MapPriorityScore(input.PriorityText);

            bool isNew = entity == null;
            if (isNew)
            {
                var initStatus = mappedStatus ?? (resolvedEmpId.HasValue ? RequestStatus.Atandi : RequestStatus.Yeni);
                entity = new ServiceRequest
                {
                    TenantId = AbpSession.TenantId ?? 1,
                    Source = input.Source,
                    ExternalRef = string.IsNullOrWhiteSpace(input.ExternalRef) ? null : input.ExternalRef.Trim(),
                    Status = initStatus,
                    // Portal zaten kapalı/çözüldü gönderdiyse ilerleme %100 (aksi halde statüye göre taban)
                    CompletionPercentage = ProgressForStatus(initStatus, 0),
                    PriorityScore = ClampScore(mappedScore ?? 5),
                    Priority = ScoreToPriority(mappedScore ?? 5),
                    // Sunucu Kurulum talepleri varsayılan "Kurulum" (portal tip göndermediyse); sonra elle değiştirilebilir
                    ActivityType = !string.IsNullOrWhiteSpace(input.ActivityType) ? input.ActivityType
                                   : (input.Source == RequestSource.SunucuKurulum ? "Kurulum" : null),
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
            // Portalın HAM durum etiketini sakla (9'lu destek durumu; bizim 7'liye eşlenirken kaybolan detay burada durur).
            // (B3) kod ("in_progress") veya etiket ("İşlemde") gelse de katalog ETİKETİNE normalize et → dropdown/rozet tutarlı
            if (!string.IsNullOrWhiteSpace(input.StatusText)) entity.PortalStatusText = PortalStatusCatalog.LabelOf(input.StatusText.Trim());
            if (mappedScore.HasValue) entity.PriorityScore = ClampScore(mappedScore.Value);
            bool wasUnassigned = !entity.AssignedEmployeeId.HasValue;
            // Atanan boşsa ve portal artık eşleşen kişi veriyorsa doldur (manuel atamayı EZMEZ; yalnız null'ı doldurur).
            if (!entity.AssignedEmployeeId.HasValue && resolvedEmpId.HasValue) entity.AssignedEmployeeId = resolvedEmpId;
            if (!entity.TeamId.HasValue && resolvedTeamId.HasValue) entity.TeamId = resolvedTeamId;

            // DURUM: PORTAL AUTHORITATIVE (tam ayna). Talebin yaşam döngüsü portalda; biz aynasıyız.
            // Her senkronda portalın GÜNCEL durumu yerele yansıtılır — durum bizde DÜZENLENMEZ (tek yerel işlem: efor).
            // Portal durumu metinden eşlenemezse (null) mevcut korunur.
            if (mappedStatus.HasValue && entity.Status != mappedStatus.Value)
            {
                entity.Status = mappedStatus.Value;
                entity.CompletionPercentage = ProgressForStatus(mappedStatus.Value, entity.CompletionPercentage);
                if (mappedStatus == RequestStatus.Cozuldu)
                {
                    entity.ResolvedDate = input.ResolvedDate ?? entity.ResolvedDate ?? DateTime.Now;
                    entity.ClosedDate = null;
                }
                else if (mappedStatus == RequestStatus.Kapandi)
                {
                    entity.ResolvedDate = input.ResolvedDate ?? entity.ResolvedDate ?? DateTime.Now;
                    entity.ClosedDate = entity.ClosedDate ?? DateTime.Now;
                }
                else if (mappedStatus == RequestStatus.Iptal)
                {
                    entity.ClosedDate = entity.ClosedDate ?? DateTime.Now;
                }
                else // Yeni/Atandı/Devam/Beklemede → yeniden açıldı: kapanış/çözüm tarihlerini temizle
                {
                    entity.ResolvedDate = null;
                    entity.ClosedDate = null;
                }
            }

            await CurrentUnitOfWork.SaveChangesAsync();

            // YENİ + AÇIK bir talep bir personele atandıysa zil bildirimi (portaldan gelen). Backfill'deki eski/kapalı
            // talepler bildirilmez (isOpen filtresi); her senkronda tekrar bildirilmez (yalnız yeni/ilk atamada).
            bool newlyAssigned = entity.AssignedEmployeeId.HasValue && (isNew || wasUnassigned);
            bool isOpenReq = entity.Status != RequestStatus.Kapandi && entity.Status != RequestStatus.Iptal && entity.Status != RequestStatus.Cozuldu;
            // B9: yalnız GERÇEKTEN YENİ (son 2 günde gelen) talepleri bildir → ilk backfill'de eski açık taleplerin bildirim seli olmaz.
            bool recent = (entity.ReceivedDate ?? DateTime.Now) >= DateTime.Now.AddDays(-2);
            if (newlyAssigned && isOpenReq && recent)
                await _notificationManager.NotifyAsync(entity.AssignedEmployeeId, NotificationType.TalepAtandi,
                    "Yeni talep atandı", entity.Title, $"/Requests/Detail/{entity.Id}", severity: "info");

            return entity.Id;
        }

        private static string Trunc(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

        // (C12) Portal dosya ekini SUNUCU-İÇİ indirir (token tarayıcıya sızmaz). Görünürlük kontrolü yapılır;
        // yalnız bu talebe kayıtlı ekin URL'si çekilir (SSRF yok — keyfi URL kabul edilmez). Kaynak yazma/okuma
        // ucuyla AYNI kimlik (API anahtarı + varsa X-User-Email) kullanılır.
        public async Task<PortalFileDto> DownloadPortalAttachmentAsync(long requestId, long attachmentId)
        {
            // 1) Görünürlük: kullanıcı bu talebi görebiliyor mu?
            var r = await ApplyVisibilityScope(_requestRepository.GetAll().AsNoTracking())
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == requestId);
            if (r == null) throw new UserFriendlyException("Talep bulunamadı veya erişim yetkiniz yok.");

            var att = r.Attachments?.FirstOrDefault(a => a.Id == attachmentId);
            if (att == null || string.IsNullOrWhiteSpace(att.Url))
                throw new UserFriendlyException("Dosya bulunamadı.");

            // 2) Kaynak kimliği (token) — talebin geldiği portal
            var src = await _sourceRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(s => s.Source == r.Source);
            if (src == null) throw new UserFriendlyException("Entegrasyon kaynağı bulunamadı.");
            var apiKey = ActivityManagement.Security.DpapiProtector.Unprotect(src.ApiKey);
            var authHeader = string.IsNullOrWhiteSpace(src.AuthHeader) ? "Authorization" : src.AuthHeader;
            var authScheme = src.AuthScheme ?? "";

            // GÜVENLİK (SSRF/token sızması): att.Url PORTAL verisidir → körü körüne çekilmez.
            // Yalnız https + host allow-list (kaynağın BaseUrl host'u + bilinen portal dosya backend'i) +
            // özel/loopback IP reddi. Token yalnız bu doğrulamayı geçen host'a gönderilir.
            if (!IsAllowedPortalFileUrl(src.BaseUrl, att.Url, out var fileUri))
                throw new UserFriendlyException("Dosya adresi güvenlik doğrulamasını geçemedi.");

            // 3) Sunucu-içi indir
            using var req = new HttpRequestMessage(HttpMethod.Get, fileUri);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var val = string.IsNullOrWhiteSpace(authScheme) ? apiKey : authScheme.Trim() + " " + apiKey;
                req.Headers.TryAddWithoutValidation(authHeader, val);
            }
            if (!string.IsNullOrWhiteSpace(src.UserEmail))
                req.Headers.TryAddWithoutValidation("X-User-Email", src.UserEmail.Trim());

            var client = _httpClientFactory.CreateClient("PortalSync");
            client.Timeout = TimeSpan.FromSeconds(60);
            using var resp = await client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            var contentType = resp.Content.Headers.ContentType?.ToString() ?? att.ContentType ?? "application/octet-stream";
            var fileName = !string.IsNullOrWhiteSpace(att.FileName) ? att.FileName
                          : (resp.Content.Headers.ContentDisposition?.FileNameStar ?? resp.Content.Headers.ContentDisposition?.FileName ?? "dosya");
            return new PortalFileDto { Content = bytes, ContentType = contentType, FileName = fileName.Trim('"') };
        }

        // (C12) Portal DETAY aynası: talebin yorum + dosya + durumunu içe aktarır. Dedup: ExternalCommentId/
        // ExternalAttachmentId. Durum PORTAL AUTHORITATIVE (yerelde kapalıysa geri açmaz). Talep yoksa atlanır.
        public async Task IngestPortalDetailAsync(PortalRequestDetailDto detail)
        {
            if (detail == null || string.IsNullOrWhiteSpace(detail.ExternalRef)) return;
            var extRef = detail.ExternalRef.Trim();
            var entity = await _requestRepository.GetAll()
                .Include(r => r.Comments).Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Source == detail.Source && r.ExternalRef == extRef);
            if (entity == null) return; // talep henüz senkronlanmamış → atla (bir sonraki liste pull'unda gelir)

            // Durum aynası (Çözüldü/Kapandı/İptal + ara durumlar; yerelde kapalıysa dokunma)
            if (!string.IsNullOrWhiteSpace(detail.StatusText)) entity.PortalStatusText = PortalStatusCatalog.LabelOf(detail.StatusText.Trim());
            var mapped = MapStatusText(detail.StatusText);
            if (mapped.HasValue && entity.Status != mapped.Value
                && entity.Status != RequestStatus.Kapandi && entity.Status != RequestStatus.Iptal)
            {
                entity.Status = mapped.Value;
                entity.CompletionPercentage = ProgressForStatus(mapped.Value, entity.CompletionPercentage);
            }

            // Yorumlar (dedup ExternalCommentId)
            var seenC = new System.Collections.Generic.HashSet<string>(
                entity.Comments.Where(c => c.ExternalCommentId != null).Select(c => c.ExternalCommentId),
                StringComparer.OrdinalIgnoreCase);
            foreach (var c in detail.Comments ?? new System.Collections.Generic.List<PortalCommentDto>())
            {
                if (string.IsNullOrWhiteSpace(c.Id) || !seenC.Add(c.Id)) continue;
                entity.Comments.Add(new ServiceRequestComment
                {
                    TenantId = entity.TenantId, ServiceRequestId = entity.Id, ExternalCommentId = c.Id,
                    AuthorName = Trunc(c.Author, 256), AuthorEmail = Trunc(c.AuthorEmail, 256),
                    CommentDate = c.Date, Body = c.Body, IsInternal = c.IsInternal
                });
            }
            // Dosya ekleri (dedup ExternalAttachmentId)
            var seenA = new System.Collections.Generic.HashSet<string>(
                entity.Attachments.Where(a => a.ExternalAttachmentId != null).Select(a => a.ExternalAttachmentId),
                StringComparer.OrdinalIgnoreCase);
            foreach (var a in detail.Attachments ?? new System.Collections.Generic.List<PortalAttachmentDto>())
            {
                if (string.IsNullOrWhiteSpace(a.Id) || !seenA.Add(a.Id)) continue;
                entity.Attachments.Add(new ServiceRequestAttachment
                {
                    TenantId = entity.TenantId, ServiceRequestId = entity.Id, ExternalAttachmentId = a.Id,
                    FileName = Trunc(a.Name, 512), Url = Trunc(a.Url, 1024), SizeBytes = a.SizeBytes,
                    ContentType = Trunc(a.ContentType, 256), UploadedAt = a.UploadedAt
                });
            }
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        // Portal e-postasını personele eşler. Önce TAM eşleşme; bulunamazsa DOMAIN'İ gözardı edip
        // yerel-parça (ön ek) ile eşler: aykut.ayvaz@tdv.org (PSM) ↔ aykut.ayvaz@cmit.com.tr (bizde).
        private async Task<long?> ResolveEmployeeByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var e = email.Trim().ToLowerInvariant();
            var local = EmailLocalPart(e);
            var emps = await _employeeRepository.GetAll().AsNoTracking()
                .Where(x => x.Email != null)
                .Select(x => new { x.Id, x.Email }).ToListAsync();
            // 1) Tam e-posta eşleşmesi
            var exact = emps.FirstOrDefault(x => string.Equals(x.Email, e, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.Id;
            // 2) Yerel-parça (ön ek) eşleşmesi — domain farkı gözardı
            if (!string.IsNullOrEmpty(local))
            {
                var byLocal = emps.FirstOrDefault(x => EmailLocalPart(x.Email) == local);
                if (byLocal != null) return byLocal.Id;
            }
            return null;
        }

        private static string EmailLocalPart(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "";
            var s = email.Trim().ToLowerInvariant();
            var at = s.IndexOf('@');
            return at > 0 ? s.Substring(0, at) : s;
        }

        private async Task<long?> ResolveTeamByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var n = name.Trim();
            return await _teamRepository.GetAll().AsNoTracking()
                .Where(t => t.Name == n)
                .Select(t => (long?)t.Id).FirstOrDefaultAsync();
        }

        // Portal durum metnini bizim RequestStatus'a eşler. Destek (Cortex) kodları/adları BİREBİR (kullanıcı listesi),
        // ardından genel TR/EN substring (PSM + varyantlar). Kod ya da ad gelebilir → ikisi de kapsanır.
        private static RequestStatus? MapStatusText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            // Türkçe 'İ' (U+0130) ToLowerInvariant'ta birleşik nokta üretip eşleşmeyi bozuyor ("İşlemde"/"İptal")
            // → önce düz 'i'ye çevir.
            var t = text.Trim().Replace('İ', 'i').ToLowerInvariant();

            // Destek durum KODLARI + adları (id: 2 open … 6 closed)
            switch (t)
            {
                case "open": case "açık": case "acik": return RequestStatus.Yeni;
                case "in_progress": case "işlemde": case "islemde": return RequestStatus.DevamEdiyor;
                case "pending": case "beklemede": return RequestStatus.Beklemede;
                case "transferred_to_dev": case "yazılım departmanına aktarıldı": case "yazilim departmanina aktarildi": return RequestStatus.Beklemede;
                case "waiting_for_customer": case "müşteriden yanıt bekleniyor": case "musteriden yanit bekleniyor": return RequestStatus.Beklemede;
                case "waiting_for_vendor": case "firmadan yanıt bekleniyor": case "firmadan yanit bekleniyor": return RequestStatus.Beklemede;
                case "resolved": case "çözüldü": case "cozuldu": return RequestStatus.Cozuldu;
                case "rejected": case "uygun görülmedi": case "uygun gorulmedi": return RequestStatus.Iptal;
                case "closed": case "kapatıldı": case "kapatildi": case "kapandı": case "kapandi": return RequestStatus.Kapandi;
            }

            // Genel substring (PSM / diğer varyantlar / eski davranış)
            if (t.Contains("iptal") || t.Contains("cancel") || t.Contains("reject")) return RequestStatus.Iptal;
            if (t.Contains("kapat") || t.Contains("kapan") || t.Contains("tamamlan") || t.Contains("closed") || t.Contains("complete") || t.Contains("done")) return RequestStatus.Kapandi;
            if (t.Contains("çöz") || t.Contains("coz") || t.Contains("resolve")) return RequestStatus.Cozuldu;
            if (t.Contains("bekle") || t.Contains("pending") || t.Contains("hold") || t.Contains("waiting")) return RequestStatus.Beklemede;
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
            // Destek öncelikleri (4): urgent/Acil→10, high/Yüksek→7, medium/Orta→5, low/Düşük→3 (1-10 sistemimize eşlenir)
            if (t.Contains("krit") || t.Contains("acil") || t.Contains("critical") || t.Contains("urgent")) return 10;
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
            // Portal talebinde GERÇEK portal durumunu göster (destek 9'lu); yoksa/manuelde bizim durum metni.
            dto.PortalStatusText = r.PortalStatusText;
            dto.StatusText = !string.IsNullOrWhiteSpace(r.ExternalRef) && !string.IsNullOrWhiteSpace(r.PortalStatusText)
                ? r.PortalStatusText
                : StatusText(r.Status);
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
            // Efor: atanan/2. sorumlu VEYA kapsamındaki yönetici (Admin/Manager/TakımLideri) girebilir.
            dto.CanLogEffort = dto.CanManage
                || (ctx.EmployeeId.HasValue && (r.AssignedEmployeeId == ctx.EmployeeId || r.SecondaryEmployeeId == ctx.EmployeeId));

            // Portal aynası (yalnız GetAsync'te Include edilir; listelerde boş kalır)
            if (r.Comments != null && r.Comments.Count > 0)
                dto.Comments = r.Comments.OrderBy(c => c.CommentDate).Select(c => new RequestCommentDto
                {
                    AuthorName = c.AuthorName, AuthorEmail = c.AuthorEmail, CommentDate = c.CommentDate,
                    Body = c.Body, IsInternal = c.IsInternal
                }).ToList();
            if (r.Attachments != null && r.Attachments.Count > 0)
                dto.Attachments = r.Attachments.OrderBy(a => a.UploadedAt).Select(a => new RequestAttachmentDto
                {
                    Id = a.Id, FileName = a.FileName, Url = a.Url, SizeBytes = a.SizeBytes, ContentType = a.ContentType, UploadedAt = a.UploadedAt
                }).ToList();
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
