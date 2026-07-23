using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class Team : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public long? LeaderId { get; set; }
        public virtual Employee Leader { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Employee> Members { get; set; } = new List<Employee>();
        public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
