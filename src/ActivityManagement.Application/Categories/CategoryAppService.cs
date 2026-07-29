using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Categories.Dto;
using ActivityManagement.Entities;

namespace ActivityManagement.Categories
{
    public class CategoryAppService : ActivityManagementAppServiceBase, ICategoryAppService
    {
        private readonly IRepository<Category, long> _categoryRepository;
        private readonly IRepository<SubCategory, long> _subCategoryRepository;
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CategoryAppService(
            IRepository<Category, long> categoryRepository,
            IRepository<SubCategory, long> subCategoryRepository,
            IRepository<Employee, long> employeeRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _employeeRepository = employeeRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        // Ana kategoriler sabittir (migration ile tanımlanır): sadece Admin sorumlu/takım atayabilir,
        // isim/liste değiştirilemez. Alt kategorileri Admin (her takım) ve TakımLideri (sadece kendi
        // takımının ana kategorisine bağlı alt kategoriler) yönetebilir.
        private string CurrentRole() =>
            _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";

        private bool IsAdmin() => string.Equals(CurrentRole(), "Admin", StringComparison.OrdinalIgnoreCase);

        private bool IsManager() =>
            IsAdmin() || string.Equals(CurrentRole(), "TakımLideri", StringComparison.OrdinalIgnoreCase);

        private long? CurrentEmployeeId()
        {
            var c = _httpContextAccessor.HttpContext?.User?.FindFirst("EmployeeId")?.Value;
            return long.TryParse(c, out var id) ? id : (long?)null;
        }

        private long? CurrentEmployeeTeamId()
        {
            var empId = CurrentEmployeeId();
            if (!empId.HasValue) return null;
            return _employeeRepository.GetAll().Where(e => e.Id == empId.Value).Select(e => e.TeamId).FirstOrDefault();
        }

        // Admin her ana kategorinin altına alt kategori ekleyebilir/düzenleyebilir/silebilir;
        // TakımLideri sadece kendi takımına ait ana kategorinin altındakileri.
        private async Task EnsureCanManageSubCategoriesAsync(long categoryId)
        {
            if (IsAdmin()) return;
            if (!IsManager())
                throw new UserFriendlyException("Alt kategori yönetme yetkiniz yok. Yalnızca Admin/Takım Lideri yapabilir.");

            var categoryTeamId = await _categoryRepository.GetAll()
                .Where(c => c.Id == categoryId)
                .Select(c => c.TeamId)
                .FirstOrDefaultAsync();

            if (categoryTeamId.HasValue && categoryTeamId != CurrentEmployeeTeamId())
                throw new UserFriendlyException("Bu ana kategori sizin takımınıza ait değil, alt kategorilerini yönetemezsiniz.");
        }

        public async Task<List<CategoryDto>> GetAllAsync(bool onlyActive = false)
        {
            IQueryable<Category> query = _categoryRepository.GetAll().AsNoTracking()
                .Include(c => c.ResponsibleEmployee1)
                .Include(c => c.ResponsibleEmployee2)
                .Include(c => c.Team)
                .Include(c => c.SubCategories);

            if (onlyActive) query = query.Where(c => c.IsActive);

            var list = await query.OrderBy(c => c.Name).ToListAsync();
            return list.Select(MapToCategoryDto).ToList();
        }

        public async Task<CategoryDto> GetAsync(long id)
        {
            var category = await _categoryRepository.GetAll().AsNoTracking()
                .Include(c => c.ResponsibleEmployee1)
                .Include(c => c.ResponsibleEmployee2)
                .Include(c => c.Team)
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == id);

            return category == null ? null : MapToCategoryDto(category);
        }

        // V4: Admin yeni ana kategori ekleyebilir.
        public async Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto input)
        {
            if (!IsAdmin())
                throw new UserFriendlyException("Ana kategori ekleme yalnızca Admin tarafından yapılabilir.");
            if (string.IsNullOrWhiteSpace(input.Name))
                throw new UserFriendlyException("Kategori adı zorunludur.");

            var category = new Category
            {
                TenantId = AbpSession.TenantId ?? 1,
                Name = input.Name.Trim(),
                Description = input.Description,
                ResponsibleEmployee1Id = input.ResponsibleEmployee1Id,
                ResponsibleEmployee2Id = input.ResponsibleEmployee2Id,
                TeamId = input.TeamId,
                IsActive = input.IsActive
            };
            await _categoryRepository.InsertAsync(category);
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(category.Id);
        }

        // V4: Admin ana kategori adını, sorumlularını, takımını ve aktifliğini düzenleyebilir.
        public async Task<CategoryDto> UpdateAsync(CreateUpdateCategoryDto input)
        {
            if (!IsAdmin())
                throw new UserFriendlyException("Ana kategoriyi yalnızca Admin düzenleyebilir.");

            var category = await _categoryRepository.GetAsync(input.Id);
            if (!string.IsNullOrWhiteSpace(input.Name)) category.Name = input.Name.Trim();
            category.Description = input.Description;
            category.ResponsibleEmployee1Id = input.ResponsibleEmployee1Id;
            category.ResponsibleEmployee2Id = input.ResponsibleEmployee2Id;
            category.IsActive = input.IsActive;
            category.TeamId = input.TeamId;
            await CurrentUnitOfWork.SaveChangesAsync();
            return await GetAsync(category.Id);
        }

        // V4: Admin ana kategori silebilir. Alt kategorisi olan kategori silinemez (önce alt kategoriler kaldırılmalı).
        public async Task DeleteAsync(long id)
        {
            if (!IsAdmin())
                throw new UserFriendlyException("Ana kategori silme yalnızca Admin tarafından yapılabilir.");
            var hasSub = await _subCategoryRepository.GetAll().AnyAsync(sc => sc.CategoryId == id);
            if (hasSub)
                throw new UserFriendlyException("Bu kategorinin alt kategorileri var. Önce alt kategorileri silin veya kategoriyi pasife alın.");
            await _categoryRepository.DeleteAsync(id);
        }

        public async Task<List<SubCategoryDto>> GetAllSubCategoriesAsync(long? categoryId = null, bool onlyActive = false)
        {
            var query = _subCategoryRepository.GetAll().AsNoTracking().Include(sc => sc.Category).AsQueryable();

            if (categoryId.HasValue) query = query.Where(sc => sc.CategoryId == categoryId.Value);
            if (onlyActive) query = query.Where(sc => sc.IsActive);

            var list = await query.OrderBy(sc => sc.Name).ToListAsync();
            return list.Select(MapToSubCategoryDto).ToList();
        }

        public async Task<SubCategoryDto> CreateSubCategoryAsync(CreateUpdateSubCategoryDto input)
        {
            await EnsureCanManageSubCategoriesAsync(input.CategoryId);

            var subCategory = new SubCategory
            {
                Name = input.Name,
                Description = input.Description,
                CategoryId = input.CategoryId,
                IsActive = input.IsActive
            };
            await _subCategoryRepository.InsertAsync(subCategory);
            await CurrentUnitOfWork.SaveChangesAsync();

            var created = await _subCategoryRepository.GetAll()
                .Include(sc => sc.Category)
                .FirstAsync(sc => sc.Id == subCategory.Id);
            return MapToSubCategoryDto(created);
        }

        public async Task<SubCategoryDto> UpdateSubCategoryAsync(CreateUpdateSubCategoryDto input)
        {
            var subCategory = await _subCategoryRepository.GetAsync(input.Id);
            await EnsureCanManageSubCategoriesAsync(subCategory.CategoryId);
            if (input.CategoryId != subCategory.CategoryId)
                await EnsureCanManageSubCategoriesAsync(input.CategoryId);
            subCategory.Name = input.Name;
            subCategory.Description = input.Description;
            subCategory.CategoryId = input.CategoryId;
            subCategory.IsActive = input.IsActive;
            await CurrentUnitOfWork.SaveChangesAsync();

            var updated = await _subCategoryRepository.GetAll()
                .Include(sc => sc.Category)
                .FirstAsync(sc => sc.Id == subCategory.Id);
            return MapToSubCategoryDto(updated);
        }

        public async Task DeleteSubCategoryAsync(long id)
        {
            var subCategory = await _subCategoryRepository.GetAsync(id);
            await EnsureCanManageSubCategoriesAsync(subCategory.CategoryId);

            await _subCategoryRepository.DeleteAsync(id);
        }

        private static CategoryDto MapToCategoryDto(Category c)
        {
            return new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ResponsibleEmployee1Id = c.ResponsibleEmployee1Id,
                ResponsibleEmployee1Name = c.ResponsibleEmployee1?.FullName,
                ResponsibleEmployee2Id = c.ResponsibleEmployee2Id,
                ResponsibleEmployee2Name = c.ResponsibleEmployee2?.FullName,
                IsActive = c.IsActive,
                TeamId = c.TeamId,
                TeamName = c.Team?.Name,
                SubCategories = c.SubCategories?
                    .Select(sc => new SubCategoryDto
                    {
                        Id = sc.Id,
                        Name = sc.Name,
                        Description = sc.Description,
                        CategoryId = sc.CategoryId,
                        CategoryName = c.Name,
                        IsActive = sc.IsActive
                    })
                    .OrderBy(sc => sc.Name)
                    .ToList() ?? new List<SubCategoryDto>()
            };
        }

        private static SubCategoryDto MapToSubCategoryDto(SubCategory sc)
        {
            return new SubCategoryDto
            {
                Id = sc.Id,
                Name = sc.Name,
                Description = sc.Description,
                CategoryId = sc.CategoryId,
                CategoryName = sc.Category?.Name,
                IsActive = sc.IsActive
            };
        }
    }
}
