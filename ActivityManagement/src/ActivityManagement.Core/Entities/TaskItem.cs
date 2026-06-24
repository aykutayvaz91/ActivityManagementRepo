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

    public enum ActivityType
    {
        Bakim = 0,
        Gelistirme = 1,
        Kurulum = 2,
        Destek = 3,
        Test = 4,
        Dokumantasyon = 5,
        Egitim = 6,
        Analiz = 7,
        Proje = 8,
        Diger = 9
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

        // 2. Sorumlu (yedek)
        public long? SecondaryEmployeeId { get; set; }
        public virtual Employee SecondaryEmployee { get; set; }

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

        // Görev grubu (üst görevler için: "Sistem Birimi", "Network Birimi", "Ortak")
        public string GroupName { get; set; }

        // Üst görev hiyerarşisi
        public long? ParentTaskId { get; set; }
        public virtual TaskItem ParentTask { get; set; }
        public virtual ICollection<TaskItem> SubTasks { get; set; } = new List<TaskItem>();

        // Alt görev aktivite tipi
        public ActivityType? ActivityType { get; set; }

        public bool IsRoutine { get; set; }
        public long? RoutineTaskId { get; set; }
        public virtual RoutineTask RoutineTask { get; set; }

        public virtual ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public virtual ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
        public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    }
}
