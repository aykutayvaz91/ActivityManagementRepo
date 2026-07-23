using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using ActivityManagement.Activities.Dto;

namespace ActivityManagement.Activities
{
    public interface IActivityLogAppService : IApplicationService
    {
        Task<PagedResultDto<ActivityLogDto>> GetAllAsync(GetActivitiesInput input);
        Task<ActivityLogDto> CreateAsync(CreateActivityLogDto input);
        Task DeleteAsync(long id);
        Task<List<ActivityLogDto>> GetEmployeeActivitiesAsync(long employeeId, DateTime startDate, DateTime endDate);
    }
}
