using System;
using System.ComponentModel.DataAnnotations;
using Abp.AutoMapper;
using ActivityManagement.Entities;

namespace ActivityManagement.Tasks.Dto
{
    [AutoMapTo(typeof(TaskItem))]
    [AutoMapFrom(typeof(TaskItemDto))]
    public class CreateUpdateTaskItemDto
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string Title { get; set; }

        [MaxLength(2000)]
        public string Description { get; set; }

        [MaxLength(256)]
        public string Category { get; set; }

        public long? ProjectId { get; set; }
        public long? AssignedEmployeeId { get; set; }
        public long? SecondaryEmployeeId { get; set; }
        public long? AssignedByEmployeeId { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public Entities.TaskStatus Status { get; set; } = Entities.TaskStatus.Beklemede;
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;

        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }
        public int CompletionPercentage { get; set; }
        public bool IsRoutine { get; set; }
        public long? RoutineTaskId { get; set; }

        // Görev grubu
        public string GroupName { get; set; }

        // Üst görev hiyerarşisi
        public long? ParentTaskId { get; set; }

        // Aktivite tipi (alt görevler için)
        public Entities.ActivityType? ActivityType { get; set; }
    }
}
