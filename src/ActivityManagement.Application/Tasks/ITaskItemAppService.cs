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
        Task<long> AddCommentAsync(long taskId, string comment, bool isInternal = false);
        Task DeleteCommentAsync(long commentId);
        // Göreve efor (harcanan süre) girer — giriş yapan kişi adına; ActualHours senkronlanır.
        Task<long> LogEffortAsync(ActivityManagement.Activities.Dto.CreateActivityLogDto input);
        Task SetApprovalAsync(long id, Entities.TaskApprovalStatus status);
        Task<long> AddAttachmentAsync(long taskId, long? taskCommentId, string fileName, string filePath, long fileSize, string contentType);
        Task<ListResultDto<TaskItemDto>> GetEmployeeTasksAsync(long employeeId);
        Task<ListResultDto<TaskItemDto>> GetCalendarTasksAsync(long employeeId, int year, int month);
    }
}
