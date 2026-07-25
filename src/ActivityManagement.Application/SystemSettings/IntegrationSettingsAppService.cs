using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;
using ActivityManagement.SystemSettings.Dto;

namespace ActivityManagement.SystemSettings
{
    // Entegrasyon ayarları (webhook + pull kaynakları). Yalnız Admin görüntüler/değiştirir.
    public class IntegrationSettingsAppService : ActivityManagementAppServiceBase, IIntegrationSettingsAppService
    {
        private readonly IRepository<IntegrationSettings, int> _settingsRepo;
        private readonly IRepository<IntegrationSource, int> _sourceRepo;
        private readonly IHttpContextAccessor _http;

        public IntegrationSettingsAppService(
            IRepository<IntegrationSettings, int> settingsRepo,
            IRepository<IntegrationSource, int> sourceRepo,
            IHttpContextAccessor http)
        {
            _settingsRepo = settingsRepo;
            _sourceRepo = sourceRepo;
            _http = http;
        }

        private void EnsureAdmin()
        {
            var role = _http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UserFriendlyException("Entegrasyon ayarlarını yalnızca Admin yönetebilir.");
        }

        private async Task<IntegrationSettings> GetOrCreateAsync()
        {
            var s = await _settingsRepo.GetAll().FirstOrDefaultAsync();
            if (s == null)
            {
                s = new IntegrationSettings { TenantId = AbpSession.TenantId ?? 1, SyncEnabled = false, IntervalMinutes = 10 };
                await _settingsRepo.InsertAsync(s);
                await CurrentUnitOfWork.SaveChangesAsync();
            }
            return s;
        }

        // Kaynak satırı yoksa (seed öncesi) oluşturur.
        private async Task<IntegrationSource> GetOrCreateSourceAsync(RequestSource src)
        {
            var s = await _sourceRepo.GetAll().FirstOrDefaultAsync(x => x.Source == src);
            if (s == null)
            {
                s = new IntegrationSource { TenantId = AbpSession.TenantId ?? 1, Source = src, Enabled = false, AuthHeader = "Authorization", AuthScheme = "Bearer", InitialLookbackDays = 7 };
                await _sourceRepo.InsertAsync(s);
                await CurrentUnitOfWork.SaveChangesAsync();
            }
            return s;
        }

        public async Task<IntegrationSettingsDto> GetAsync()
        {
            EnsureAdmin();
            var s = await GetOrCreateAsync();
            await GetOrCreateSourceAsync(RequestSource.SunucuKurulum);
            await GetOrCreateSourceAsync(RequestSource.DisDestek);

            var sources = await _sourceRepo.GetAll().AsNoTracking().OrderBy(x => x.Source).ToListAsync();
            string baseUrl = null;
            try { baseUrl = _http.HttpContext?.Request is { } r ? $"{r.Scheme}://{r.Host}" : null; } catch { }

            return new IntegrationSettingsDto
            {
                HasInboundKey = !string.IsNullOrWhiteSpace(s.InboundApiKey),
                WebhookUrl = (baseUrl ?? "https://activitymanagement.tdv.org") + "/api/integration/requests",
                SyncEnabled = s.SyncEnabled,
                IntervalMinutes = s.IntervalMinutes,
                Sources = sources.Select(x => new IntegrationSourceDto
                {
                    Id = x.Id,
                    Source = x.Source,
                    SourceText = x.Source == RequestSource.SunucuKurulum ? "Sunucu Kurulum (psm.tdv.org)" : "Dış Destek (destek.cmit.com.tr)",
                    Enabled = x.Enabled,
                    BaseUrl = x.BaseUrl,
                    HasApiKey = !string.IsNullOrWhiteSpace(x.ApiKey),
                    AuthHeader = x.AuthHeader,
                    AuthScheme = x.AuthScheme,
                    Filter = x.Filter,
                    InitialLookbackDays = x.InitialLookbackDays,
                    LastSyncUtc = x.LastSyncUtc,
                    LastRunUtc = x.LastRunUtc,
                    LastResult = x.LastResult
                }).ToList()
            };
        }

        public async Task SaveGeneralAsync(string inboundApiKey, bool syncEnabled, int intervalMinutes, bool clearInboundKey = false)
        {
            EnsureAdmin();
            var s = await GetOrCreateAsync();
            // "Temizle" işaretliyse anahtar silinir (webhook kapanır); değilse boş bırakılırsa korunur.
            if (clearInboundKey) s.InboundApiKey = null;
            else if (!string.IsNullOrWhiteSpace(inboundApiKey)) s.InboundApiKey = inboundApiKey.Trim();
            s.SyncEnabled = syncEnabled;
            s.IntervalMinutes = intervalMinutes < 1 ? 1 : (intervalMinutes > 1440 ? 1440 : intervalMinutes);
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        public async Task SaveSourceAsync(int id, bool enabled, string baseUrl, string apiKey,
                                          string authHeader, string authScheme, string filter, int initialLookbackDays)
        {
            EnsureAdmin();
            var s = await _sourceRepo.GetAsync(id);
            s.Enabled = enabled;
            s.BaseUrl = baseUrl?.Trim();
            if (!string.IsNullOrWhiteSpace(apiKey)) s.ApiKey = apiKey.Trim();  // boşsa değişmez
            s.AuthHeader = string.IsNullOrWhiteSpace(authHeader) ? "Authorization" : authHeader.Trim();
            s.AuthScheme = authScheme?.Trim() ?? "";
            s.Filter = filter?.Trim();
            s.InitialLookbackDays = initialLookbackDays < 0 ? 0 : (initialLookbackDays > 365 ? 365 : initialLookbackDays);
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        public async Task<(bool Enabled, string Key)> GetInboundAsync()
        {
            var s = await _settingsRepo.GetAll().AsNoTracking().FirstOrDefaultAsync();
            var key = s?.InboundApiKey;
            return (!string.IsNullOrWhiteSpace(key), key);
        }
    }
}
