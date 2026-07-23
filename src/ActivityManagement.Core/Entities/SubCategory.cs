using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class SubCategory : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public long CategoryId { get; set; }
        public virtual Category Category { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
