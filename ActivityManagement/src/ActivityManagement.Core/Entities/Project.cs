using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public enum ProjectStatus
    {
        Planlandi = 0,
        Devam = 1,
        Tamamlandi = 2,
        Iptal = 3
    }

    public class Project : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planlandi;
        public int Priority { get; set; } = 1;

        public long? ManagerId { get; set; }
        public virtual Employee Manager { get; set; }

        public virtual ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
