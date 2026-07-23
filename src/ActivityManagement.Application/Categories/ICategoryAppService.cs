using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Categories.Dto;

namespace ActivityManagement.Categories
{
    public interface ICategoryAppService : IApplicationService
    {
        Task<List<CategoryDto>> GetAllAsync(bool onlyActive = false);
        Task<CategoryDto> GetAsync(long id);
        Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto input);
        Task<CategoryDto> UpdateAsync(CreateUpdateCategoryDto input);
        Task DeleteAsync(long id);

        Task<List<SubCategoryDto>> GetAllSubCategoriesAsync(long? categoryId = null, bool onlyActive = false);
        Task<SubCategoryDto> CreateSubCategoryAsync(CreateUpdateSubCategoryDto input);
        Task<SubCategoryDto> UpdateSubCategoryAsync(CreateUpdateSubCategoryDto input);
        Task DeleteSubCategoryAsync(long id);
    }
}
