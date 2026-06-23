using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Authorization;
using ActivityManagement.Entities;
using ActivityManagement.Tasks.Dto;

namespace ActivityManagement.Tasks
{
    [AbpAuthorize(ActivityManagementPermissions.Tasks.Default)]
    public class TaskItemAppService : ActivityManagementAppServiceBase, ITaskItemAppService
    {
        private readonly IRepository<TaskItem, long> _taskRepository;
        private readonly IRepository<TaskComment, long> _commentRepository;

        public TaskItemAppService(
            IRepository<TaskItem, long> taskRepository,
            IRepository<TaskComment, long> commentRepository)
        {
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
        }

        public async Task<PagedResultDto<TaskItemDto>> GetAllAsync(GetTasksInput input)
        {
            var query = _taskRepository.GetAll()
                .Include(t => t.Project)
                .Include(t => t.AssignedEmployee)
                .Include(t => t.AssignedByEmployee)
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), t => t.Title.Contains(input.Filter))
                .WhereIf(input.ProjectId.HasValue, t => t.ProjectId == input.ProjectId.Value)
                .WhereIf(input.AssignedEmployeeId.HasValue, t => t.AssignedEmployeeId == input.AssignedEmployeeId.Value)
                .WhereIf(input.Status.HasValue, t => t.Status == input.Status.Value)
                .WhereIf(input.Priority.HasValue, t => t.Priority == input.Priority.Value);

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(t => t.CreationTime).PageBy(input).ToListAsync();

            return new PagedResultDto<TaskItemDto>(count, items.Select(MapToDto).ToList());
        }

        public async Task<TaskItemDto> GetAsync(long id)
        {
            var task = await _taskRepository.GetAll()
                .Include(t => t.Project)
                .Include(t => t.AssignedEmployee)
                .Include(t => t.AssignedByEmployee)
                .Include(t => t.Comments)
                .Include(t => t.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);
            return MapToDto(task);
        }

        [AbpAuthorize(ActivityManagementPermissions.Tasks.Create)]
        public async Task<TaskItemDto> CreateAsync(CreateUpdateTaskItemDto input)
        {
            var task = ObjectMapper.Map<TaskItem>(input);
            task.TenantId = AbpSession.TenantId ?? 1;
            await _taskRepository.InsertAsync(task);
            await CurrentUnitOfWork.SaveChangesAsync();
            return MapToDto(task);
        }

        [AbpAuthorize(ActivityManagementPermissions.Tasks.Edit)]
        public async Task<TaskItemDto> UpdateAsync(CreateUpdateTaskItemDto input)
        {
            var task = await _taskRepository.GetAsync(input.Id);
            ObjectMapper.Map(input, task);
            return MapToDto(task);
        }

        [AbpAuthorize(ActivityManagementPermissions.Tasks.Delete)]
        public async Task DeleteAsync(long id)
        {
            await _taskRepository.DeleteAsync(id);
        }

        public async Task UpdateStatusAsync(long id, Entities.TaskStatus status, int percentage)
        {
            var task = await _taskRepository.GetAsync(id);
            task.Status = status;
            task.CompletionPercentage = percentage;
            if (status == Entities.TaskStatus.Tamamlandi)
                task.CompletedDate = DateTime.Now;
        }

        public async Task AddCommentAsync(long taskId, string comment)
        {
            await _commentRepository.InsertAsync(new TaskComment
            {
                TaskItemId = taskId,
                Comment = comment,
                AuthorName = AbpSession.UserId.HasValue ? $"Kullanıcı #{AbpSession.UserId}" : "Bilinmiyor",
                TenantId = AbpSession.TenantId ?? 1
            });
        }

        public async Task<ListResultDto<TaskItemDto>> GetEmployeeTasksAsync(long employeeId)
        {
            var tasks = await _taskRepository.GetAll()
                .Include(t => t.Project)
                .Where(t => t.AssignedEmployeeId == employeeId)
                .OrderByDescending(t => t.DueDate)
                .ToListAsync();
            return new ListResultDto<TaskItemDto>(tasks.Select(MapToDto).ToList());
        }

        public async Task<ListResultDto<TaskItemDto>> GetCalendarTasksAsync(long employeeId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var tasks = await _taskRepository.GetAll()
                .Include(t => t.Project)
                .Where(t => t.AssignedEmployeeId == employeeId &&
                            t.DueDate >= startDate && t.DueDate <= endDate)
                .ToListAsync();
            return new ListResultDto<TaskItemDto>(tasks.Select(MapToDto).ToList());
        }

        private TaskItemDto MapToDto(TaskItem t)
        {
            if (t == null) return null;
            var dto = ObjectMapper.Map<TaskItemDto>(t);
            dto.ProjectName = t.Project?.Name;
            dto.AssignedEmployeeName = t.AssignedEmployee?.FullName;
            dto.AssignedByEmployeeName = t.AssignedByEmployee?.FullName;
            dto.StatusText = t.Status.ToString();
            dto.PriorityText = t.Priority.ToString();
            dto.Comments = t.Comments?.Select(c => new TaskCommentDto
            {
                Id = c.Id, Comment = c.Comment, AuthorName = c.AuthorName, CreationTime = c.CreationTime
            }).ToList() ?? new List<TaskCommentDto>();
            dto.Attachments = t.Attachments?.Select(a => new TaskAttachmentDto
            {
                Id = a.Id, FileName = a.FileName, FilePath = a.FilePath,
                FileSize = a.FileSize, ContentType = a.ContentType
            }).ToList() ?? new List<TaskAttachmentDto>();
            return dto;
        }
    }
}
