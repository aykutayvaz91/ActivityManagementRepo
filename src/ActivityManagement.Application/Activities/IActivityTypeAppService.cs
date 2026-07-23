using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using ActivityManagement.Activities.Dto;

namespace ActivityManagement.Activities
{
    public interface IActivityTypeAppService : IApplicationService
    {
        Task<List<ActivityTypeDefDto>> GetAllAsync(bool onlyActive = false);
        Task<ActivityTypeDefDto> CreateAsync(CreateUpdateActivityTypeDto input);
        Task<ActivityTypeDefDto> UpdateAsync(CreateUpdateActivityTypeDto input);
        Task DeleteAsync(int id);
    }
}
