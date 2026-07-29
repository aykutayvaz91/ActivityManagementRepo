using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;

namespace ActivityManagement.Authorization
{
    public interface IAccessControlAppService : IApplicationService
    {
        // GÜVENLİK: okuma metotları dynamic API'ye AÇILMAZ (rol yapısı/erişim matrisi sızmasın) — yalnız
        // sunucu-içi çağrılır (base controller ACL + Admin ekranı). Mutasyonlar zaten EnsureAdmin ile korunuyor.
        // Bir rolün erişebildiği sayfa anahtarları (Admin => tümü).
        [Abp.Application.Services.RemoteService(false)]
        Task<List<string>> GetAllowedPagesAsync(string role);
        [Abp.Application.Services.RemoteService(false)]
        Task<List<AppRoleDefDto>> GetRolesAsync();
        [Abp.Application.Services.RemoteService(false)]
        Task<AccessMatrixDto> GetMatrixAsync();
        // allowedByRole: rol -> işaretli sayfa anahtarları. (Admin salt-okunur; sistem Admin her zaman tümü.)
        Task SaveMatrixAsync(Dictionary<string, List<string>> allowedByRole);
        Task CreateRoleAsync(string name, string displayName);
        Task DeleteRoleAsync(int id);
    }
}
