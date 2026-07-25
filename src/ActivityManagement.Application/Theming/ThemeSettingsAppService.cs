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
        private readonly IRepository<Employee, long> _employeeRepo;
        private readonly IRepository<Team, long> _teamRepo;
        private readonly IHttpContextAccessor _http;

        public ThemeSettingsAppService(
            IRepository<ThemeSettings, int> repo,
            IRepository<Employee, long> employeeRepo,
            IRepository<Team, long> teamRepo,
            IHttpContextAccessor http)
        {
            _repo = repo;
            _employeeRepo = employeeRepo;
            _teamRepo = teamRepo;
            _http = http;
        }

        // Giriş yapan kişinin takımının kısa adı (ör. INFRA). Sistem Yöneticisi → "ADMIN". Yoksa null.
        private async Task<string> CurrentTeamShortNameAsync()
        {
            var v = _http.HttpContext?.User?.FindFirst("EmployeeId")?.Value;
            if (!long.TryParse(v, out var empId)) return null;
            var emp = await _employeeRepo.GetAll().AsNoTracking()
                .Where(e => e.Id == empId)
                .Select(e => new { e.IsSystemAccount, e.TeamId }).FirstOrDefaultAsync();
            if (emp == null) return null;
            if (emp.IsSystemAccount) return "ADMIN"; // Sistem Yöneticisi'nin "takımı" ADMIN → üst menüde ADMIN yazar
            if (!emp.TeamId.HasValue) return "FYS"; // takımsız (ör. Manager) → uzun ad yerine kısa marka "FYS"
            var sn = await _teamRepo.GetAll().AsNoTracking()
                .Where(t => t.Id == emp.TeamId.Value).Select(t => t.ShortName).FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(sn) ? "FYS" : sn; // takım kısa adı yoksa da "FYS"
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
                return new ThemeSettingsDto { EffectiveBrand = "Faaliyet Yönetim Sistemi" };

            var dto = new ThemeSettingsDto
            {
                PrimaryColor = t.PrimaryColor,
                LogoUrl = t.LogoUrl,
                BrandName = t.BrandName,
                UseTeamNameAsBrand = t.UseTeamNameAsBrand
            };
            // Efektif marka: toggle açık + kullanıcının takım kısa adı varsa onu, yoksa BrandName.
            string teamShort = t.UseTeamNameAsBrand ? await CurrentTeamShortNameAsync() : null;
            dto.EffectiveBrand = !string.IsNullOrWhiteSpace(teamShort) ? teamShort : t.BrandName;
            return dto;
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
            t.UseTeamNameAsBrand = input.UseTeamNameAsBrand;
            await CurrentUnitOfWork.SaveChangesAsync();
        }
    }
}
