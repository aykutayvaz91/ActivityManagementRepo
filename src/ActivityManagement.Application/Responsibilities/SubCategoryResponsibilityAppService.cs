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
using ActivityManagement.Responsibilities.Dto;

namespace ActivityManagement.Responsibilities
{
    // Alt kategori sorumluluk matrisi. Yalnızca Admin/TakımLideri yönetir (manuel claim kontrolü).
    public class SubCategoryResponsibilityAppService : ActivityManagementAppServiceBase, ISubCategoryResponsibilityAppService
    {
        private readonly IRepository<SubCategoryResponsibility, long> _repo;
        private readonly IRepository<SubCategory, long> _subCategoryRepo;
        private readonly IHttpContextAccessor _http;

        public SubCategoryResponsibilityAppService(
            IRepository<SubCategoryResponsibility, long> repo,
            IRepository<SubCategory, long> subCategoryRepo,
            IHttpContextAccessor http)
        {
            _repo = repo;
            _subCategoryRepo = subCategoryRepo;
            _http = http;
        }

        private (string Role, long? EmployeeId) Ctx()
        {
            var u = _http.HttpContext?.User;
            var role = u?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            long? emp = long.TryParse(u?.FindFirst("EmployeeId")?.Value, out var id) ? id : (long?)null;
            return (role, emp);
        }

        // NOT: base'teki IsManager (Admin/Manager/TakımLideri) DEĞİL — burada KASITLI olarak "Manager" hariç
        // (sorumluluk matrisini yalnız Admin + TakımLideri yönetir). new ile bilinçli gizleme.
        private new bool IsManager(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);

        private static string TypeText(ResponsibilityType t) => t == ResponsibilityType.Primary ? "Asıl Sorumlu" : "Yedek Sorumlu";

        private static SubCategoryResponsibilityDto Map(SubCategoryResponsibility r) => new SubCategoryResponsibilityDto
        {
            Id = r.Id,
            SubCategoryId = r.SubCategoryId,
            SubCategoryName = r.SubCategory?.Name,
            CategoryId = r.SubCategory?.CategoryId ?? 0,
            CategoryName = r.SubCategory?.Category?.Name,
            EmployeeId = r.EmployeeId,
            EmployeeName = r.Employee?.FullName,
            ResponsibilityType = r.ResponsibilityType,
            TypeText = TypeText(r.ResponsibilityType)
        };

        private IQueryable<SubCategoryResponsibility> WithIncludes(IQueryable<SubCategoryResponsibility> q) =>
            q.Include(r => r.SubCategory).ThenInclude(sc => sc.Category).Include(r => r.Employee);

        public async Task<List<SubCategoryResponsibilityDto>> GetAllAsync()
        {
            var items = await WithIncludes(_repo.GetAll().AsNoTracking())
                .OrderBy(r => r.SubCategory.CategoryId).ThenBy(r => r.SubCategoryId).ThenBy(r => r.ResponsibilityType)
                .ToListAsync();
            return items.Select(Map).ToList();
        }

        public async Task<List<SubCategoryResponsibilityDto>> GetByEmployeeAsync(long employeeId)
        {
            var items = await WithIncludes(_repo.GetAll().AsNoTracking())
                .Where(r => r.EmployeeId == employeeId)
                .OrderBy(r => r.ResponsibilityType).ThenBy(r => r.SubCategoryId)
                .ToListAsync();
            return items.Select(Map).ToList();
        }

        public async Task SetAsync(SetResponsibilityInput input)
        {
            var ctx = Ctx();
            if (!IsManager(ctx.Role))
                throw new UserFriendlyException("Sorumluluk ataması yalnızca Admin/Takım Lideri tarafından yapılabilir.");
            if (input.EmployeeId <= 0 || input.SubCategoryId <= 0)
                throw new UserFriendlyException("Alt kategori ve personel seçilmelidir.");

            // Aynı (alt kategori + personel) varsa tipini güncelle, yoksa ekle
            var existing = await _repo.FirstOrDefaultAsync(r => r.SubCategoryId == input.SubCategoryId && r.EmployeeId == input.EmployeeId);
            if (existing != null)
            {
                existing.ResponsibilityType = input.ResponsibilityType;
                existing.AssignedByTeamLeaderId = ctx.EmployeeId;
            }
            else
            {
                await _repo.InsertAsync(new SubCategoryResponsibility
                {
                    TenantId = AbpSession.TenantId ?? 1,
                    SubCategoryId = input.SubCategoryId,
                    EmployeeId = input.EmployeeId,
                    ResponsibilityType = input.ResponsibilityType,
                    AssignedByTeamLeaderId = ctx.EmployeeId
                });
            }
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        public async Task RemoveAsync(long id)
        {
            var ctx = Ctx();
            if (!IsManager(ctx.Role))
                throw new UserFriendlyException("Sorumluluk silme yalnızca Admin/Takım Lideri tarafından yapılabilir.");
            await _repo.DeleteAsync(id);
        }
    }
}
