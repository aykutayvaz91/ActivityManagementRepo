using System.Threading.Tasks;
using Abp.Application.Services;

namespace ActivityManagement.Theming
{
    public interface IThemeSettingsAppService : IApplicationService
    {
        Task<ThemeSettingsDto> GetAsync();
        Task UpdateAsync(ThemeSettingsDto input);
    }
}
