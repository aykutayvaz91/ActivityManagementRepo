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
                try { intervalMinutes = await RunOnceAsync(); }
                catch (Exception ex) { ActivityManagement.Logging.ErrorLog.Write(ex, "RequestSyncHostedService"); }

                var delay = TimeSpan.FromMinutes(intervalMinutes < 1 ? 1 : intervalMinutes);
                try { await Task.Delay(delay, stoppingToken); }
                catch { break; }
            }
        }

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
            string baseUrl, apiKey, authHeader, authScheme, filter;
            RequestSource source;
            DateTime sinceUtc;
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
                filter = src.Filter;
                sinceUtc = src.LastSyncUtc ?? DateTime.UtcNow.AddDays(-Math.Max(0, src.InitialLookbackDays));
            }

            var runStartUtc = DateTime.UtcNow;
            string result;
            int count = 0;
            try
            {
                // HTTP çek (UoW dışı)
                var items = await FetchAsync(baseUrl, apiKey, authHeader, authScheme, filter, sinceUtc);

                // Upsert (UoW içinde)
                using var scope = _serviceProvider.CreateScope();
                var sp = scope.ServiceProvider;
                var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
                var reqSvc = sp.GetRequiredService<IServiceRequestAppService>();
                var sourceRepo = sp.GetRequiredService<IRepository<IntegrationSource, int>>();
                using var uow = uowManager.Begin();
                foreach (var wire in items)
                {
                    var dto = MapWire(wire, source);
                    if (dto == null || string.IsNullOrWhiteSpace(dto.Title)) continue;
                    await reqSvc.UpsertFromPortalAsync(dto);
                    count++;
                }
                // Watermark + durum güncelle
                var src = await sourceRepo.GetAsync(sourceId);
                src.LastSyncUtc = runStartUtc;
                src.LastRunUtc = DateTime.Now;
                src.LastResult = $"OK: {count} kayıt";
                await uow.CompleteAsync();
                result = src.LastResult;
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

        private async Task<List<WireItem>> FetchAsync(string baseUrl, string apiKey, string authHeader, string authScheme, string filter, DateTime sinceUtc)
        {
            var sb = new StringBuilder(baseUrl);
            sb.Append(baseUrl.Contains("?") ? "&" : "?");
            sb.Append("updatedSince=").Append(Uri.EscapeDataString(sinceUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")));
            if (!string.IsNullOrWhiteSpace(filter)) sb.Append("&").Append(filter.TrimStart('&', '?'));

            using var req = new HttpRequestMessage(HttpMethod.Get, sb.ToString());
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var val = string.IsNullOrWhiteSpace(authScheme) ? apiKey : authScheme.Trim() + " " + apiKey;
                req.Headers.TryAddWithoutValidation(authHeader, val);
            }

            var client = _httpClientFactory.CreateClient("PortalSync");
            client.Timeout = TimeSpan.FromSeconds(30);
            using var resp = await client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return new List<WireItem>();

            // Yanıt {items:[...]} zarfı VEYA doğrudan [...] dizisi olabilir.
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith("["))
                return JsonSerializer.Deserialize<List<WireItem>>(json, JsonOpts) ?? new List<WireItem>();
            var env = JsonSerializer.Deserialize<WireResponse>(json, JsonOpts);
            return env?.Items ?? new List<WireItem>();
        }

        private static PortalRequestDto MapWire(WireItem w, RequestSource source)
        {
            if (w == null) return null;
            var extra = BuildExtraInfo(w);
            return new PortalRequestDto
            {
                Source = source,
                ExternalRef = w.ExternalRef,
                ExternalUrl = w.Url,
                Title = w.Title,
                Description = w.Description,
                RequesterName = w.RequesterName,
                RequesterEmail = w.RequesterEmail,
                AssigneeEmail = w.AssigneeEmail,
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
        private class WireResponse { public List<WireItem> Items { get; set; } public string NextPage { get; set; } }
        private class WireItem
        {
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
            public DateTime? UpdatedAt { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public DateTime? DueDate { get; set; }
            public DateTime? ResolvedAt { get; set; }
            public DateTime? ClosedAt { get; set; }
            public Dictionary<string, JsonElement> Requested { get; set; }
            public Dictionary<string, JsonElement> Installed { get; set; }
            public List<Dictionary<string, JsonElement>> Services { get; set; }
        }
    }
}
