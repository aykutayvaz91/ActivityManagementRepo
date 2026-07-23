using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Entities;

namespace ActivityManagement.Authorization
{
    // Rol × Sayfa erişim yönetimi. Okuma açık (menü/enforcement); yazma yalnız Admin.
    public class AccessControlAppService : ActivityManagementAppServiceBase, IAccessControlAppService
    {
        private readonly IRepository<AppRoleDef, int> _roleRepo;
        private readonly IRepository<RolePageAccess, int> _accessRepo;
        private readonly IRepository<Employee, long> _employeeRepo;
        private readonly IHttpContextAccessor _http;

        public AccessControlAppService(
            IRepository<AppRoleDef, int> roleRepo,
            IRepository<RolePageAccess, int> accessRepo,
            IRepository<Employee, long> employeeRepo,
            IHttpContextAccessor http)
        {
            _roleRepo = roleRepo;
            _accessRepo = accessRepo;
            _employeeRepo = employeeRepo;
            _http = http;
        }

        private bool IsAdmin()
        {
            var role = _http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureAdmin()
        {
            if (!IsAdmin()) throw new UserFriendlyException("Bu işlem yalnızca Admin tarafından yapılabilir.");
        }

        private static readonly List<string> AllPageKeys = PageCatalog.Pages.Select(p => p.Key).ToList();

        public async Task<List<string>> GetAllowedPagesAsync(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) role = "Uzman";
            // Admin her sayfaya erişir (kilitlenme olmasın).
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return new List<string>(AllPageKeys);

            var rows = await _accessRepo.GetAll().AsNoTracking()
                .Where(a => a.RoleName == role).ToListAsync();

            if (rows.Any())
                return rows.Where(a => a.Allowed).Select(a => a.PageKey).ToList();

            // Kaydı yoksa: sistem rolü için varsayılan; değilse boş (özel rol admin izin verene dek erişemez).
            if (PageCatalog.DefaultAccess.TryGetValue(role, out var def))
                return def.ToList();
            return new List<string>();
        }

        public async Task<List<AppRoleDefDto>> GetRolesAsync()
        {
            var roles = await _roleRepo.GetAll().AsNoTracking()
                .OrderByDescending(r => r.IsSystem).ThenBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync();
            return roles.Select(Map).ToList();
        }

        public async Task<AccessMatrixDto> GetMatrixAsync()
        {
            var dto = new AccessMatrixDto
            {
                Pages = PageCatalog.Pages.Select(p => new PageDefDto { Key = p.Key, Title = p.Title }).ToList(),
                Roles = (await GetRolesAsync())
            };
            foreach (var r in dto.Roles)
                dto.Allowed[r.Name] = await GetAllowedPagesAsync(r.Name);
            return dto;
        }

        public async Task SaveMatrixAsync(Dictionary<string, List<string>> allowedByRole)
        {
            EnsureAdmin();
            if (allowedByRole == null) return;

            var roles = await _roleRepo.GetAll().AsNoTracking().ToListAsync();
            foreach (var role in roles)
            {
                // Admin her zaman tüm sayfalara erişir; matriste değiştirilemez.
                if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase)) continue;

                var wanted = allowedByRole.TryGetValue(role.Name, out var list) ? new HashSet<string>(list) : new HashSet<string>();
                var current = await _accessRepo.GetAll().Where(a => a.RoleName == role.Name).ToListAsync();
                var currentByKey = current.ToDictionary(a => a.PageKey, a => a);

                foreach (var page in PageCatalog.Pages)
                {
                    bool allow = wanted.Contains(page.Key);
                    if (currentByKey.TryGetValue(page.Key, out var row))
                    {
                        if (row.Allowed != allow) { row.Allowed = allow; await _accessRepo.UpdateAsync(row); }
                    }
                    else
                    {
                        await _accessRepo.InsertAsync(new RolePageAccess { RoleName = role.Name, PageKey = page.Key, Allowed = allow });
                    }
                }
            }
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        public async Task CreateRoleAsync(string name, string displayName)
        {
            EnsureAdmin();
            if (string.IsNullOrWhiteSpace(name)) throw new UserFriendlyException("Rol adı zorunludur.");
            name = name.Trim();
            if (await _roleRepo.GetAll().AnyAsync(r => r.Name == name))
                throw new UserFriendlyException("Bu adda bir rol zaten var.");
            await _roleRepo.InsertAsync(new AppRoleDef
            {
                Name = name,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(),
                IsSystem = false,
                SortOrder = 10
            });
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        public async Task DeleteRoleAsync(int id)
        {
            EnsureAdmin();
            var role = await _roleRepo.GetAsync(id);
            if (role.IsSystem)
                throw new UserFriendlyException("Sistem rolleri silinemez.");
            var inUse = await _employeeRepo.GetAll().AnyAsync(e => e.AppRole == role.Name);
            if (inUse)
                throw new UserFriendlyException("Bu rol bazı personellere atanmış; önce onları başka role taşıyın.");

            var rows = await _accessRepo.GetAll().Where(a => a.RoleName == role.Name).ToListAsync();
            foreach (var r in rows) await _accessRepo.DeleteAsync(r);
            await _roleRepo.DeleteAsync(id);
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        private static AppRoleDefDto Map(AppRoleDef r) => new AppRoleDefDto
        {
            Id = r.Id, Name = r.Name, DisplayName = r.DisplayName, IsSystem = r.IsSystem, SortOrder = r.SortOrder
        };
    }
}
