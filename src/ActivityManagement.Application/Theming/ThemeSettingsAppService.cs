using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;

namespace ActivityManagement.Theming
{
    // Tema ayarları (tek satır). Okuma herkese açık (layout kullanır); güncelleme yalnız Admin.
    public class ThemeSettingsAppService : ActivityManagementAppServiceBase, IThemeSettingsAppService
    {
        private readonly IRepository<ThemeSettings, int> _repo;
        private readonly IHttpContextAccessor _http;

        public ThemeSettingsAppService(IRepository<ThemeSettings, int> repo, IHttpContextAccessor http)
        {
            _repo = repo;
            _http = http;
        }

        private bool IsAdmin()
        {
            var role = _http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ThemeSettingsDto> GetAsync()
        {
            var t = await _repo.GetAll().AsNoTracking().FirstOrDefaultAsync();
            if (t == null)
                return new ThemeSettingsDto();
            return new ThemeSettingsDto { PrimaryColor = t.PrimaryColor, LogoUrl = t.LogoUrl, BrandName = t.BrandName };
        }

        public async Task UpdateAsync(ThemeSettingsDto input)
        {
            if (!IsAdmin())
                throw new UserFriendlyException("Tema ayarlarını yalnızca Admin değiştirebilir.");

            var t = await _repo.FirstOrDefaultAsync(x => true);
            if (t == null)
            {
                t = new ThemeSettings();
                await _repo.InsertAsync(t);
            }
            if (!string.IsNullOrWhiteSpace(input.PrimaryColor)) t.PrimaryColor = input.PrimaryColor.Trim();
            if (input.LogoUrl != null) t.LogoUrl = input.LogoUrl;
            if (!string.IsNullOrWhiteSpace(input.BrandName)) t.BrandName = input.BrandName.Trim();
            await CurrentUnitOfWork.SaveChangesAsync();
        }
    }
}
