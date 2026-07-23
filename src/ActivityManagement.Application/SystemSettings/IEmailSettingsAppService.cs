using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.SystemSettings.Dto;

namespace ActivityManagement.SystemSettings
{
    public interface IEmailSettingsAppService : IApplicationService
    {
        Task<EmailSettingsDto> GetAsync();
        Task<EmailSettingsDto> UpdateAsync(UpdateEmailSettingsDto input);
        Task SendTestEmailAsync(string toEmail);
    }
}
