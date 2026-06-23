using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using ActivityManagement.Tasks.Dto;

namespace ActivityManagement.Tasks
{
    public interface ITaskItemAppService : IApplicationService
    {
        Task<PagedResultDto<TaskItemDto>> GetAllAsync(GetTasksInput input);
        Task<TaskItemDto> GetAsync(long id);
        Task<TaskItemDto> CreateAsync(CreateUpdateTaskItemDto input);
        Task<TaskItemDto> UpdateAsync(CreateUpdateTaskItemDto input);
        Task DeleteAsync(long id);
        Task UpdateStatusAsync(long id, Entities.TaskStatus status, int percentage);
        Task AddCommentAsync(long taskId, string comment);
        Task<ListResultDto<TaskItemDto>> GetEmployeeTasksAsync(long employeeId);
        Task<ListResultDto<TaskItemDto>> GetCalendarTasksAsync(long employeeId, int year, int month);
    }
}
