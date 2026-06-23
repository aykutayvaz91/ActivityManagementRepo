using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public enum RecurrenceType
    {
        Gunluk = 0,
        Haftalik = 1,
        Aylik = 2,
        Ozel = 3
    }

    public class RoutineTask : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        public long EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public RecurrenceType RecurrenceType { get; set; }
        public string RecurrenceDays { get; set; } // JSON: [1,3,5] for Mon,Wed,Fri
        public int RecurrenceDay { get; set; }     // day of month for monthly

        public TimeSpan? StartTime { get; set; }
        public decimal EstimatedHours { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        public virtual ICollection<TaskItem> GeneratedTasks { get; set; } = new List<TaskItem>();
    }
}
