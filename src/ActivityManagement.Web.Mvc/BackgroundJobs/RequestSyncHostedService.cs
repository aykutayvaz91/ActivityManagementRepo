using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ActivityManagement.Entities;
using ActivityManagement.ServiceRequests;
using ActivityManagement.ServiceRequests.Dto;

namespace ActivityManagement.Web.BackgroundJobs
{
    // FAZ 2 — Talep PULL senkronizasyonu. Ayarlar DB'de (IntegrationSettings/IntegrationSources), admin yönetir.
    // Varsayılan KAPALI: SyncEnabled=false veya kaynak Enabled=false ise hiçbir dış çağrı yapılmaz.
    // Portal okuma ucundan JSON çeker → PortalRequestDto → ServiceRequestAppService.UpsertFromPortalAsync (idempotent).
    public class RequestSyncHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // H5 — art arda başarısızlık takibi: eşiğe ulaşınca admin(ler)e bir kez uyarı (başarıda sıfırlanır → spam yok).
        private int _consecutiveFailures;
        private bool _adminAlerted;
        private const int FailureAlertThreshold = 3;

        public RequestSyncHostedService(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch { }

            while (!stoppingToken.IsCancellationRequested)
            {
                int intervalMinutes = 10;
                try
                {
                    intervalMinutes = await RunOnceAsync();
                    // Başarılı tur → sayaç ve uyarı bayrağı sıfırlanır.
                    _consecutiveFailures = 0;
                    _adminAlerted = false;
                }
                catch (Exception ex)
                {
                    ActivityManagement.Logging.ErrorLog.Write(ex, "RequestSyncHostedService");
                    _consecutiveFailures++;
                    if (_consecutiveFailures >= FailureAlertThreshold && !_adminAlerted)
                    {
                        _adminAlerted = true; // aynı hata serisinde bir kez
                        try { await AlertAdminsAsync(_consecutiveFailures, ex); }
                        catch (Exception aex) { ActivityManagement.Logging.ErrorLog.Write(aex, "RequestSyncHostedService/AlertAdmins"); }
                    }
                }

                var delay = TimeSpan.FromMinutes(intervalMinutes < 1 ? 1 : intervalMinutes);
                try { await Task.Delay(delay, stoppingToken); }
                catch { break; }
            }
        }

        // Talep senkronu art arda başarısız → tüm aktif Admin'lere in-app bildirim (danger).
        private async Task AlertAdminsAsync(int failures, Exception ex)
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;
            var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
            var empRepo = sp.GetRequiredService<IRepository<Employee, long>>();
            var notifier = sp.GetRequiredService<ActivityManagement.Notifications.INotificationManager>();

            using var uow = uowManager.Begin();
            var admins = await empRepo.GetAll().AsNoTracking()
                .Where(e => e.IsActive && e.AppRole == "Admin")
                .Select(e => e.Id).ToListAsync();
            var msg = $"Talep senkronizasyonu {failures} turdur başarısız. Son hata: {Trunc(ex?.Message, 160)}. Entegrasyon ayarlarını/portalı kontrol edin.";
            foreach (var adminId in admins)
                await notifier.NotifyAsync(adminId, Entities.NotificationType.Genel,
                    "Senkron hatası", msg, "/Admin/Integration", icon: "fa-triangle-exclamation", severity: "danger");
            await uow.CompleteAsync();
        }

        private static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? s : (s.Length <= n ? s : s.Substring(0, n) + "…");

        // Bir tur: aktif kaynakları çeker. Dönen değer bir sonraki bekleme (dakika).
        private async Task<int> RunOnceAsync()
        {
            // 1) Genel ayarı + aktif kaynakları oku (kısa UoW).
            bool syncEnabled; int intervalMinutes;
            List<int> activeSourceIds;
            using (var scope = _serviceProvider.CreateScope())
            {
                var sp = scope.ServiceProvider;
                var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
                var settingsRepo = sp.GetRequiredService<IRepository<IntegrationSettings, int>>();
                var sourceRepo = sp.GetRequiredService<IRepository<IntegrationSource, int>>();
                using var uow = uowManager.Begin();
                var settings = await settingsRepo.GetAll().AsNoTracking().FirstOrDefaultAsync();
                syncEnabled = settings?.SyncEnabled ?? false;
                intervalMinutes = settings?.IntervalMinutes ?? 10;
                activeSourceIds = syncEnabled
                    ? await sourceRepo.GetAll().AsNoTracking()
                        .Where(s => s.Enabled && s.BaseUrl != null && s.BaseUrl != "")
                        .Select(s => s.Id).ToListAsync()
                    : new List<int>();
                await uow.CompleteAsync();
            }

            if (!syncEnabled || activeSourceIds.Count == 0) return intervalMinutes;

            foreach (var sourceId in activeSourceIds)
                await SyncSourceAsync(sourceId);

            return intervalMinutes;
        }

        private async Task SyncSourceAsync(int sourceId)
        {
            // Kaynak yapılandırmasını oku
            string baseUrl, apiKey, authHeader, authScheme, filter, userEmail;
            RequestSource source;
            DateTime sinceUtc;
            bool detailSyncEnabled;
            using (var scope = _serviceProvider.CreateScope())
            {
                var sp = scope.ServiceProvider;
                var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
                var sourceRepo = sp.GetRequiredService<IRepository<IntegrationSource, int>>();
                using var uow = uowManager.Begin();
                var src = await sourceRepo.GetAll().AsNoTracking().FirstOrDefaultAsync(s => s.Id == sourceId);
                await uow.CompleteAsync();
                if (src == null) return;
                source = src.Source; baseUrl = src.BaseUrl;
                apiKey = ActivityManagement.Security.DpapiProtector.Unprotect(src.ApiKey); // DB'de şifreli → çöz

                authHeader = string.IsNullOrWhiteSpace(src.AuthHeader) ? "Authorization" : src.AuthHeader;
                authScheme = src.AuthScheme ?? "";
                userEmail = src.UserEmail;
                filter = src.Filter;
                detailSyncEnabled = src.DetailSyncEnabled;
                sinceUtc = src.LastSyncUtc ?? DateTime.UtcNow.AddDays(-Math.Max(0, src.InitialLookbackDays));
            }

            var runStartUtc = DateTime.UtcNow;
            string result;
            int count = 0;
            try
            {
                // HTTP çek (UoW dışı)
                var items = await FetchAsync(baseUrl, apiKey, authHeader, authScheme, userEmail, filter, sinceUtc);

                // Upsert — KÜÇÜK BATCH'ler halinde commit. Eskiden tüm kayıtlar tek UoW/transaction'da
                // commit ediliyordu; AG senkron replica commit gecikmesiyle büyük backfill'de "Execution
                // Timeout" (Error -2) alınıyor ve watermark ilerlemediği için her turda baştan denenip
                // kalıcı timeout oluşuyordu. Upsert (Source,ExternalRef) idempotent olduğundan batch'li
                // commit güvenli: yarıda kesilse bile commit'lenen batch'ler kalıcı, kalanlar bir sonraki
                // turda tekrar (ucuz) upsert edilir.
                const int CommitBatchSize = 25;
                for (int i = 0; i < items.Count; i += CommitBatchSize)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var sp = scope.ServiceProvider;
                    var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
                    var reqSvc = sp.GetRequiredService<IServiceRequestAppService>();
                    using var uow = uowManager.Begin();
                    foreach (var wire in items.Skip(i).Take(CommitBatchSize))
                    {
                        var dto = MapWire(wire, source);
                        if (dto == null || string.IsNullOrWhiteSpace(dto.Title)) continue;
                        await reqSvc.UpsertFromPortalAsync(dto);
                        count++;
                    }
                    await uow.CompleteAsync();
                }

                // (V2) Talep DETAYI: yorum + dosya + güncel durum aynası — yalnız portalın detay ucu açık
                // kaynaklarda (DetailSyncEnabled). Best-effort: bir talebin detayı alınamazsa atlanır (loglanır).
                // WATERMARK'TAN ÖNCE yapılır: tur ortasında süreç çökerse/yeniden başlarsa watermark ilerlememiş
                // olur ve sonraki tur aynı pencereyi yeniden çeker (idempotent upsert/ingest → güvenli).
                int detailFail = 0;
                if (detailSyncEnabled)
                {
                    foreach (var wire in items)
                    {
                        var extRef = !string.IsNullOrWhiteSpace(wire.ExternalRef) ? wire.ExternalRef
                                   : (wire.Id.HasValue ? wire.Id.Value.ToString() : null);
                        if (string.IsNullOrWhiteSpace(extRef)) continue;
                        try
                        {
                            var detail = await FetchDetailAsync(baseUrl, apiKey, authHeader, authScheme, userEmail, source, extRef);
                            if (detail == null) continue;
                            using var scope = _serviceProvider.CreateScope();
                            var sp = scope.ServiceProvider;
                            var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
                            var reqSvc = sp.GetRequiredService<IServiceRequestAppService>();
                            using var uow = uowManager.Begin();
                            await reqSvc.IngestPortalDetailAsync(detail);
                            await uow.CompleteAsync();
                        }
                        catch (Exception dex)
                        {
                            detailFail++;
                            ActivityManagement.Logging.ErrorLog.Write(dex, $"RequestSync/{source}/Detail/{extRef}");
                        }
                    }
                }

                // Watermark + durum (liste batch'leri + detay senkronu bitti → watermark güvenle ilerletilir).
                // Not: poison bir detay kaydında sonsuz yeniden-çekme olmasın diye kısmi hata olsa da ilerletilir (loglanır).
                using (var scope = _serviceProvider.CreateScope())
                {
                    var sp = scope.ServiceProvider;
                    var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
                    var sourceRepo = sp.GetRequiredService<IRepository<IntegrationSource, int>>();
                    using var uow = uowManager.Begin();
                    var src = await sourceRepo.GetAsync(sourceId);
                    src.LastSyncUtc = runStartUtc;
                    src.LastRunUtc = DateTime.Now;
                    src.LastResult = detailFail > 0 ? $"OK: {count} kayıt ({detailFail} detay hatası)" : $"OK: {count} kayıt";
                    await uow.CompleteAsync();
                    result = src.LastResult;
                }
            }
            catch (Exception ex)
            {
                result = "HATA: " + (ex.Message.Length > 300 ? ex.Message.Substring(0, 300) : ex.Message);
                ActivityManagement.Logging.ErrorLog.Write(ex, $"RequestSync/{source}");
                // Hata durumunu yaz (watermark ilerletilmez → sonraki turda tekrar denenir)
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var sp = scope.ServiceProvider;
                    var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
                    var sourceRepo = sp.GetRequiredService<IRepository<IntegrationSource, int>>();
                    using var uow = uowManager.Begin();
                    var src = await sourceRepo.GetAsync(sourceId);
                    src.LastRunUtc = DateTime.Now;
                    src.LastResult = result;
                    await uow.CompleteAsync();
                }
                catch { }
            }
        }

        // Tüm sayfaları çeker (sayfalama). Dedup: aynı externalRef/id ikinci kez gelirse durur (portal
        // `page`'i yok sayıp aynı sayfayı döndürse bile sonsuz döngü olmaz). Son sayfa: 0 kayıt / yeni-yok / <50.
        private const int PageSize = 50;    // portala gönderilen sayfa boyutu
        private const int MaxPages = 400;   // güvenlik cap'i (400×50 = 20000) — büyük backfill'de eksik çekmeyi önler

        private async Task<List<WireItem>> FetchAsync(string baseUrl, string apiKey, string authHeader, string authScheme, string userEmail, string filter, DateTime sinceUtc)
        {
            var all = new List<WireItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int page = 1;
            for (; page <= MaxPages; page++)
            {
                var pageItems = await FetchPageAsync(baseUrl, apiKey, authHeader, authScheme, userEmail, filter, sinceUtc, page);
                if (pageItems.Count == 0) break;
                int fresh = 0;
                foreach (var it in pageItems)
                {
                    var key = !string.IsNullOrWhiteSpace(it.ExternalRef) ? it.ExternalRef
                            : (it.Id.HasValue ? it.Id.Value.ToString() : null);
                    if (string.IsNullOrWhiteSpace(key) || seen.Add(key)) { all.Add(it); fresh++; }
                }
                if (fresh == 0) break;                  // portal page'i yok saydı / yeni kayıt yok
                if (pageItems.Count < PageSize) break;  // son sayfa
            }
            if (page > MaxPages)
                ActivityManagement.Logging.ErrorLog.Write(
                    new Exception($"Pull sayfa cap'ine ({MaxPages}) ulaşıldı — bazı kayıtlar bu turda çekilmemiş olabilir (baseUrl={baseUrl})"),
                    "RequestSync/PageCap");
            return all;
        }

        private async Task<List<WireItem>> FetchPageAsync(string baseUrl, string apiKey, string authHeader, string authScheme, string userEmail, string filter, DateTime sinceUtc, int page)
        {
            var sb = new StringBuilder(baseUrl);
            sb.Append(baseUrl.Contains("?") ? "&" : "?");
            // Artımlı senkron TEK parametre: updatedSince (portalın 'değişenleri' döndürmesi için). Not: 'from' (created)
            // ile BİRLİKTE göndermek, portal AND'lerse eski taleplerdeki durum değişimini kaçırır → yalnız updatedSince.
            sb.Append("updatedSince=").Append(Uri.EscapeDataString(sinceUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")));
            sb.Append("&page=").Append(page);
            sb.Append("&pageSize=").Append(PageSize);   // sayfa boyutunu netleştir (yoksa <50 heuristiği güvenilmez)
            if (!string.IsNullOrWhiteSpace(filter)) sb.Append("&").Append(filter.TrimStart('&', '?'));

            using var req = new HttpRequestMessage(HttpMethod.Get, sb.ToString());
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var val = string.IsNullOrWhiteSpace(authScheme) ? apiKey : authScheme.Trim() + " " + apiKey;
                req.Headers.TryAddWithoutValidation(authHeader, val);
            }
            // İkinci kimlik/aktör header'ı (PSM: X-User-Email). Doluysa gönderilir.
            if (!string.IsNullOrWhiteSpace(userEmail))
                req.Headers.TryAddWithoutValidation("X-User-Email", userEmail.Trim());

            var client = _httpClientFactory.CreateClient("PortalSync");
            client.Timeout = TimeSpan.FromSeconds(30);
            using var resp = await client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return new List<WireItem>();

            // Yanıt {items:[...]} / {data:[...]} zarfı VEYA doğrudan [...] dizisi olabilir.
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith("["))
                return JsonSerializer.Deserialize<List<WireItem>>(json, JsonOpts) ?? new List<WireItem>();
            var env = JsonSerializer.Deserialize<WireResponse>(json, JsonOpts);
            return env?.Effective ?? new List<WireItem>();
        }

        // (V2) Talep detay ucu: GET {liste-yolu}/{externalRef} → yorum + dosya + güncel durum.
        // baseUrl liste ucudur (örn .../api/talepler); detay = liste yolu + "/" + talepNo (destek & PSM aynı desen).
        // 404 → detay yok (null döner, atlanır). Diğer HTTP hataları çağırana fırlar (best-effort loglanır).
        private async Task<PortalRequestDetailDto> FetchDetailAsync(string baseUrl, string apiKey, string authHeader, string authScheme, string userEmail, RequestSource source, string externalRef)
        {
            var basePath = baseUrl.Contains("?") ? baseUrl.Substring(0, baseUrl.IndexOf('?')) : baseUrl;
            var url = basePath.TrimEnd('/') + "/" + Uri.EscapeDataString(externalRef);

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var val = string.IsNullOrWhiteSpace(authScheme) ? apiKey : authScheme.Trim() + " " + apiKey;
                req.Headers.TryAddWithoutValidation(authHeader, val);
            }
            if (!string.IsNullOrWhiteSpace(userEmail))
                req.Headers.TryAddWithoutValidation("X-User-Email", userEmail.Trim());

            var client = _httpClientFactory.CreateClient("PortalSync");
            client.Timeout = TimeSpan.FromSeconds(30);
            using var resp = await client.SendAsync(req);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return null;

            var d = JsonSerializer.Deserialize<DetailWire>(json, JsonOpts);
            if (d == null) return null;

            var dto = new PortalRequestDetailDto
            {
                Source = source,
                ExternalRef = !string.IsNullOrWhiteSpace(d.ExternalRef) ? d.ExternalRef : externalRef,
                StatusText = d.Status
            };
            if (d.Comments != null)
                foreach (var c in d.Comments)
                    dto.Comments.Add(new PortalCommentDto
                    {
                        Id = c.Id, Author = c.Author, AuthorEmail = c.AuthorEmail,
                        Date = c.Date, Body = c.Body, IsInternal = c.IsInternal
                    });
            if (d.Attachments != null)
                foreach (var a in d.Attachments)
                    dto.Attachments.Add(new PortalAttachmentDto
                    {
                        Id = a.Id, Name = a.Name, Url = a.Url, SizeBytes = a.SizeBytes,
                        ContentType = a.ContentType, UploadedAt = a.UploadedAt
                    });
            return dto;
        }

        private static PortalRequestDto MapWire(WireItem w, RequestSource source)
        {
            if (w == null) return null;
            var extra = BuildExtraInfo(w);
            return new PortalRequestDto
            {
                Source = source,
                // PSM sayısal 'id' → externalRef (yoksa)
                ExternalRef = !string.IsNullOrWhiteSpace(w.ExternalRef) ? w.ExternalRef : (w.Id.HasValue ? w.Id.Value.ToString() : null),
                ExternalUrl = w.Url,
                Title = w.Title,
                Description = w.Description,
                // destek: requesterName/Email ; PSM: requestedByName/Email
                RequesterName = w.RequesterName ?? w.RequestedByName,
                RequesterEmail = w.RequesterEmail ?? w.RequestedByEmail,
                // destek: assigneeEmail ; PSM: assignedToEmail
                AssigneeEmail = w.AssigneeEmail ?? w.AssignedToEmail,
                GroupName = w.Group,
                StatusText = w.Status,
                PriorityText = w.Priority,
                ExtraInfo = string.IsNullOrWhiteSpace(extra) ? null : extra,
                ReceivedDate = w.CreatedAt ?? w.StartedAt,
                DueDate = w.DueDate,
                ResolvedDate = w.ResolvedAt ?? w.CompletedAt
            };
        }

        // Kaynağa özel/ekstra alanları tek metinde toplar (parola/kimlik ASLA gelmez — payload'da yok).
        private static string BuildExtraInfo(WireItem w)
        {
            var parts = new List<string>();
            void Add(string label, string val) { if (!string.IsNullOrWhiteSpace(val)) parts.Add($"{label}: {val}"); }
            Add("Birim", w.RequesterUnit);
            Add("Ağ", w.Network);
            Add("Kategori", w.Category);
            Add("Sorun Tipi", w.ProblemType);
            Add("Atayan", w.AssignedByEmail);
            // PSM (Sunucu Kurulum) alanları
            Add("Atanan Kişi", w.AssignedToName);
            Add("Hostname", w.Hostname);
            Add("IP", w.IpAddress);
            Add("İşletim Sistemi", w.OsRequested);
            Add("Barındırma", w.HostingType);
            Add("Ortam", w.EnvironmentName);
            Add("Lokasyon", w.LocationName);
            var hw = new System.Collections.Generic.List<string>();
            if (w.CpuCores.HasValue) hw.Add(w.CpuCores.Value + " vCPU");
            if (w.RamGb.HasValue) hw.Add(w.RamGb.Value + " GB RAM");
            if (w.DiskGb.HasValue) hw.Add(w.DiskGb.Value + " GB Disk");
            if (hw.Count > 0) parts.Add("Donanım: " + string.Join(" / ", hw));
            if (w.Installed != null && w.Installed.Count > 0)
                parts.Add("Kurulan: " + string.Join(", ", w.Installed.Select(kv => $"{kv.Key}={ValStr(kv.Value)}")));
            if (w.Requested != null && w.Requested.Count > 0)
                parts.Add("İstenen: " + string.Join(", ", w.Requested.Select(kv => $"{kv.Key}={ValStr(kv.Value)}")));
            if (w.Services != null && w.Services.Count > 0)
                parts.Add("Servis/Port: " + string.Join(" | ", w.Services.Select(s =>
                    $"{ValStr(GetVal(s, "name"))}:{ValStr(GetVal(s, "port"))}/{ValStr(GetVal(s, "protocol"))}")));
            return string.Join("\n", parts);
        }

        private static JsonElement? GetVal(Dictionary<string, JsonElement> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? v : (JsonElement?)null;

        private static string ValStr(JsonElement? e)
        {
            if (e == null) return "";
            var v = e.Value;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                JsonValueKind.True => "evet",
                JsonValueKind.False => "hayır",
                JsonValueKind.Null => "",
                _ => v.ToString()
            };
        }

        // --- portal JSON şeması (docs §F ile uyumlu) ---
        // Zarf: items VEYA data dizisi; nextPage her tipte olabilir (string/sayı/null) → JsonElement? ile tolere edilir.
        private class WireResponse
        {
            public List<WireItem> Items { get; set; }
            public List<WireItem> Data { get; set; }
            public System.Text.Json.JsonElement? NextPage { get; set; }
            public List<WireItem> Effective => Items ?? Data;
        }
        private class WireItem
        {
            public int? Id { get; set; }               // PSM sayısal id (externalRef yoksa anahtar)
            // --- PSM (Sunucu Kurulum) alanları ---
            public string RequestedByEmail { get; set; }
            public string RequestedByName { get; set; }
            public string AssignedToEmail { get; set; }
            public string AssignedToName { get; set; }
            public string HostingType { get; set; }
            public string OsRequested { get; set; }
            public int? CpuCores { get; set; }
            public int? RamGb { get; set; }
            public int? DiskGb { get; set; }
            public string EnvironmentName { get; set; }
            public string LocationName { get; set; }
            public string Hostname { get; set; }
            public string IpAddress { get; set; }
            // --- destek (Dış Destek) / ortak alanlar ---
            public string ExternalRef { get; set; }
            public string Url { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string RequesterName { get; set; }
            public string RequesterUnit { get; set; }
            public string RequesterEmail { get; set; }
            public string AssignedByEmail { get; set; }
            public string AssigneeEmail { get; set; }
            public string Group { get; set; }
            public string Category { get; set; }
            public string ProblemType { get; set; }
            public string Priority { get; set; }
            public string Network { get; set; }
            public string Status { get; set; }
            public DateTime? CreatedAt { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public DateTime? DueDate { get; set; }
            public DateTime? ResolvedAt { get; set; }
            public Dictionary<string, JsonElement> Requested { get; set; }
            public Dictionary<string, JsonElement> Installed { get; set; }
            public List<Dictionary<string, JsonElement>> Services { get; set; }
        }

        // (V2) Detay ucu yanıtı — yorum + dosya + durum (docs: GET /api/talepler/{no}).
        private class DetailWire
        {
            public string ExternalRef { get; set; }
            public string Status { get; set; }
            public List<DetailComment> Comments { get; set; }
            public List<DetailAttachment> Attachments { get; set; }
        }
        private class DetailComment
        {
            public string Id { get; set; }
            public string Author { get; set; }
            public string AuthorEmail { get; set; }
            public DateTime? Date { get; set; }
            public string Body { get; set; }
            public bool IsInternal { get; set; }
        }
        private class DetailAttachment
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Url { get; set; }
            public long SizeBytes { get; set; }
            public string ContentType { get; set; }
            public DateTime? UploadedAt { get; set; }
        }
    }
}
