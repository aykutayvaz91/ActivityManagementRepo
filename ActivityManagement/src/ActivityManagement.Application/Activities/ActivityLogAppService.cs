using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Activities.Dto;
using ActivityManagement.Authorization;
using ActivityManagement.Entities;

namespace ActivityManagement.Activities
{
    [AbpAuthorize(ActivityManagementPermissions.Activities.Default)]
    public class ActivityLogAppService : ActivityManagementAppServiceBase, IActivityLogAppService
    {
        private readonly IRepository<ActivityLog, long> _activityRepository;

        public ActivityLogAppService(IRepository<ActivityLog, long> activityRepository)
        {
            _activityRepository = activityRepository;
        }

        public async Task<PagedResultDto<ActivityLogDto>> GetAllAsync(GetActivitiesInput input)
        {
            var query = _activityRepository.GetAll()
                .Include(a => a.Employee)
                .Include(a => a.TaskItem)
                .Include(a => a.Project)
                .WhereIf(input.EmployeeId.HasValue, a => a.EmployeeId == input.EmployeeId.Value)
                .WhereIf(input.ProjectId.HasValue, a => a.ProjectId == input.ProjectId.Value)
                .WhereIf(input.StartDate.HasValue, a => a.ActivityDate >= input.StartDate.Value)
                .WhereIf(input.EndDate.HasValue, a => a.ActivityDate <= input.EndDate.Value);

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.ActivityDate).PageBy(input).ToListAsync();
            return new PagedResultDto<ActivityLogDto>(count, items.Select(MapToDto).ToList());
        }

        [AbpAuthorize(ActivityManagementPermissions.Activities.Create)]
        public async Task<ActivityLogDto> CreateAsync(CreateActivityLogDto input)
        {
            var log = new ActivityLog
            {
                TenantId = AbpSession.TenantId ?? 1,
                EmployeeId = input.EmployeeId,
                TaskItemId = input.TaskItemId,
                ProjectId = input.ProjectId,
                Description = input.Description,
                ActivityDate = input.ActivityDate,
                HoursSpent = input.HoursSpent,
                ActivityType = input.ActivityType
            };
            await _activityRepository.InsertAsync(log);
            await CurrentUnitOfWork.SaveChangesAsync();
            return MapToDto(log);
        }

        [AbpAuthorize(ActivityManagementPermissions.Activities.Delete)]
        public async Task DeleteAsync(long id)
        {
            await _activityRepository.DeleteAsync(id);
        }

        public async Task<List<ActivityLogDto>> GetEmployeeActivitiesAsync(long employeeId, DateTime startDate, DateTime endDate)
        {
            var items = await _activityRepository.GetAll()
                .Include(a => a.TaskItem)
                .Include(a => a.Project)
                .Where(a => a.EmployeeId == employeeId && a.ActivityDate >= startDate && a.ActivityDate <= endDate)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();
            return items.Select(MapToDto).ToList();
        }

        private ActivityLogDto MapToDto(ActivityLog a)
        {
            var dto = ObjectMapper.Map<ActivityLogDto>(a);
            dto.EmployeeName = a.Employee?.FullName;
            dto.TaskTitle = a.TaskItem?.Title;
            dto.ProjectName = a.Project?.Name;
            return dto;
        }
    }
}
