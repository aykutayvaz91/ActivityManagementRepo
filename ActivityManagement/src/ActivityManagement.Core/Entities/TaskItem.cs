using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public enum TaskStatus
    {
        Beklemede = 0,
        DevamEdiyor = 1,
        Tamamlandi = 2,
        Iptal = 3,
        Ertelendi = 4
    }

    public enum TaskPriority
    {
        Dusuk = 0,
        Normal = 1,
        Yuksek = 2,
        Kritik = 3
    }

    public class TaskItem : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        // Alt kategori (xlsx'teki Alt Başlık, örn. "1.1. Sunucu ve Sanallaştırma Yönetimi")
        public string Category { get; set; }

        public long? ProjectId { get; set; }
        public virtual Project Project { get; set; }

        public long? AssignedEmployeeId { get; set; }
        public virtual Employee AssignedEmployee { get; set; }

        public long? AssignedByEmployeeId { get; set; }
        public virtual Employee AssignedByEmployee { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public TaskStatus Status { get; set; } = TaskStatus.Beklemede;
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;

        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }

        public int CompletionPercentage { get; set; }

        public bool IsRoutine { get; set; }
        public long? RoutineTaskId { get; set; }
        public virtual RoutineTask RoutineTask { get; set; }

        public virtual ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public virtual ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
        public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    }
}
