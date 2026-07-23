using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    // Faaliyet Konusu: Takım Lideri/Admin tarafından tanımlanan, bir uzmana atanan,
    // SLA'sız rutin/periyodik iş başlığı. Uzman buna efor (ActivityLog) girer.
    public class ActivitySubject : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        public long? CategoryId { get; set; }
        public virtual Category Category { get; set; }

        public long? SubCategoryId { get; set; }
        public virtual SubCategory SubCategory { get; set; }

        // Faaliyet konusu bir projeye bağlıysa kategoriler projeden miras alınır
        public long? ProjectId { get; set; }
        public virtual Project Project { get; set; }

        public long? CreatedByLeaderId { get; set; }
        public virtual Employee CreatedByLeader { get; set; }

        public long? AssignedEmployeeId { get; set; }
        public virtual Employee AssignedEmployee { get; set; }

        public long? TeamId { get; set; }
        public virtual Team Team { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<ActivityLog> Logs { get; set; } = new List<ActivityLog>();
    }
}
