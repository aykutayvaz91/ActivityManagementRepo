using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Responsibilities.Dto;

namespace ActivityManagement.Responsibilities
{
    public interface ISubCategoryResponsibilityAppService : IApplicationService
    {
        Task<List<SubCategoryResponsibilityDto>> GetAllAsync();
        Task<List<SubCategoryResponsibilityDto>> GetByEmployeeAsync(long employeeId);
        Task SetAsync(SetResponsibilityInput input);
        Task RemoveAsync(long id);
    }
}
