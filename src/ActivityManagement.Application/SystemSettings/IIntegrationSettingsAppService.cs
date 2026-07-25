using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.SystemSettings.Dto;

namespace ActivityManagement.SystemSettings
{
    public interface IIntegrationSettingsAppService : IApplicationService
    {
        // Admin ekranı
        Task<IntegrationSettingsDto> GetAsync();
        Task SaveGeneralAsync(string inboundApiKey, bool syncEnabled, int intervalMinutes, bool clearInboundKey = false);
        Task SaveSourceAsync(int id, bool enabled, string baseUrl, string apiKey,
                             string authHeader, string authScheme, string filter, int initialLookbackDays);

        // Webhook alıcısı için (yetki denetimi YOK — anonim endpoint kullanır).
        Task<(bool Enabled, string Key)> GetInboundAsync();
    }
}
