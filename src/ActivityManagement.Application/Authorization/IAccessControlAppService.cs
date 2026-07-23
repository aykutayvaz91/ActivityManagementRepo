using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;

namespace ActivityManagement.Authorization
{
    public interface IAccessControlAppService : IApplicationService
    {
        // Bir rolün erişebildiği sayfa anahtarları (Admin => tümü).
        Task<List<string>> GetAllowedPagesAsync(string role);
        Task<List<AppRoleDefDto>> GetRolesAsync();
        Task<AccessMatrixDto> GetMatrixAsync();
        // allowedByRole: rol -> işaretli sayfa anahtarları. (Admin salt-okunur; sistem Admin her zaman tümü.)
        Task SaveMatrixAsync(Dictionary<string, List<string>> allowedByRole);
        Task CreateRoleAsync(string name, string displayName);
        Task DeleteRoleAsync(int id);
    }
}
