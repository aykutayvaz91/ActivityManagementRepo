using System;
using System.Collections.Generic;
using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Tasks.Dto
{
    [AutoMapFrom(typeof(TaskItem))]
    public class TaskItemDto : FullAuditedEntityDto<long>
    {
        public int TenantId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public long? ProjectId { get; set; }
        public string ProjectName { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public string AssignedEmployeeName { get; set; }
        public long? SecondaryEmployeeId { get; set; }
        public string SecondaryEmployeeName { get; set; }
        public long? AssignedByEmployeeId { get; set; }
        public string AssignedByEmployeeName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public Entities.TaskStatus Status { get; set; }
        public string StatusText { get; set; }
        public TaskPriority Priority { get; set; }
        public string PriorityText { get; set; }
        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }
        public int CompletionPercentage { get; set; }
        public bool IsRoutine { get; set; }

        // Grup
        public string GroupName { get; set; }

        // Hiyerarşi
        public long? ParentTaskId { get; set; }
        public string ParentTaskTitle { get; set; }
        public bool IsParentTask => !ParentTaskId.HasValue;
        public List<TaskItemDto> SubTasks { get; set; } = new List<TaskItemDto>();

        // Aktivite tipi (alt görevler için)
        public Entities.ActivityType? ActivityType { get; set; }
        public string ActivityTypeText { get; set; }

        // Sunucu tarafı yetki
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        public List<TaskCommentDto> Comments { get; set; } = new List<TaskCommentDto>();
        public List<TaskAttachmentDto> Attachments { get; set; } = new List<TaskAttachmentDto>();
    }

    public class TaskCommentDto
    {
        public long Id { get; set; }
        public string Comment { get; set; }
        public string AuthorName { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public class TaskAttachmentDto
    {
        public long Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string ContentType { get; set; }
    }
}
