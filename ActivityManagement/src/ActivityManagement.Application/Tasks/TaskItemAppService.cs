using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Abp.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ActivityManagement.Authorization;
using ActivityManagement.Entities;
using ActivityManagement.Tasks.Dto;

namespace ActivityManagement.Tasks
{
    public class TaskItemAppService : ActivityManagementAppServiceBase, ITaskItemAppService
    {
        private readonly IRepository<TaskItem, long> _taskRepository;
        private readonly IRepository<TaskComment, long> _commentRepository;
        private readonly IRepository<Employee, long> _employeeRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TaskItemAppService(
            IRepository<TaskItem, long> taskRepository,
            IRepository<TaskComment, long> commentRepository,
            IRepository<Employee, long> employeeRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _employeeRepository = employeeRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        // Mevcut kullanıcının rolü ve çalışan kimliği (cookie claim'lerinden)
        private (string Role, string Email, long? EmployeeId) CurrentContext()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "Uzman";
            var email = user?.FindFirst(ClaimTypes.Email)?.Value
                        ?? user?.FindFirst(ClaimTypes.Name)?.Value;
            long? empId = null;
            if (!string.IsNullOrEmpty(email))
                empId = _employeeRepository.GetAll().FirstOrDefault(e => e.Email == email)?.Id;
            return (role, email, empId);
        }

        private bool IsManager(string role) =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "TakımLideri", StringComparison.OrdinalIgnoreCase);

        // Uzman yalnızca kendisine atanmış görevde işlem yapabilir
        private void EnsureCanModify(TaskItem task)
        {
            var ctx = CurrentContext();
            if (IsManager(ctx.Role)) return;
            if (task.AssignedEmployeeId.HasValue && ctx.EmployeeId.HasValue &&
                task.AssignedEmployeeId.Value == ctx.EmployeeId.Value) return;
            throw new UserFriendlyException("Bu görev size atanmadığı için üzerinde işlem yapamazsınız.");
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

        // Görev oluşturma ve atama yalnızca Admin / Takım Lideri
        public async Task<TaskItemDto> CreateAsync(CreateUpdateTaskItemDto input)
        {
            var ctx = CurrentContext();
            if (!IsManager(ctx.Role))
                throw new UserFriendlyException("Görev oluşturma/atama yetkiniz yok. Bu işlem Takım Lideri/Yönetici tarafından yapılır.");

            var task = ObjectMapper.Map<TaskItem>(input);
            task.TenantId = AbpSession.TenantId ?? 1;
            await _taskRepository.InsertAsync(task);
            await CurrentUnitOfWork.SaveChangesAsync();
            return MapToDto(task);
        }

        public async Task<TaskItemDto> UpdateAsync(CreateUpdateTaskItemDto input)
        {
            var task = await _taskRepository.GetAsync(input.Id);
            EnsureCanModify(task);

            // Uzman; atama, proje ve atayan bilgilerini değiştiremesin (sadece yönetici)
            var ctx = CurrentContext();
            if (!IsManager(ctx.Role))
            {
                input.AssignedEmployeeId = task.AssignedEmployeeId;
                input.AssignedByEmployeeId = task.AssignedByEmployeeId;
                input.ProjectId = task.ProjectId;
            }

            ObjectMapper.Map(input, task);
            return MapToDto(task);
        }

        public async Task DeleteAsync(long id)
        {
            var task = await _taskRepository.GetAsync(id);
            EnsureCanModify(task);
            await _taskRepository.DeleteAsync(id);
        }

        public async Task UpdateStatusAsync(long id, Entities.TaskStatus status, int percentage)
        {
            var task = await _taskRepository.GetAsync(id);
            EnsureCanModify(task);
            task.Status = status;
            task.CompletionPercentage = percentage;
            if (status == Entities.TaskStatus.Tamamlandi)
                task.CompletedDate = DateTime.Now;
        }

        public async Task AddCommentAsync(long taskId, string comment)
        {
            var task = await _taskRepository.GetAsync(taskId);
            EnsureCanModify(task);
            var ctx = CurrentContext();
            var author = ctx.EmployeeId.HasValue
                ? _employeeRepository.Get(ctx.EmployeeId.Value).FullName
                : (ctx.Email ?? "Bilinmiyor");
            await _commentRepository.InsertAsync(new TaskComment
            {
                TaskItemId = taskId,
                Comment = comment,
                AuthorName = author,
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
