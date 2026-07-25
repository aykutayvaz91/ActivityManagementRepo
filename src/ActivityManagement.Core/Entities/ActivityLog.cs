using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class ActivityLog : CreationAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public long? TaskItemId { get; set; }
        public virtual TaskItem TaskItem { get; set; }

        public long? ProjectId { get; set; }
        public virtual Project Project { get; set; }

        // Bağlı olduğu faaliyet konusu (varsa). Görev/proje eforundan bağımsız faaliyet girişleri buraya bağlanır.
        public long? ActivitySubjectId { get; set; }
        public virtual ActivitySubject ActivitySubject { get; set; }

        // Bağlı olduğu talep (varsa) — psm.tdv.org / destek.cmit.com.tr taleplerine harcanan efor.
        public long? ServiceRequestId { get; set; }
        public virtual ServiceRequest ServiceRequest { get; set; }

        public string Description { get; set; }
        public DateTime ActivityDate { get; set; }
        public decimal HoursSpent { get; set; }
        public string ActivityType { get; set; }
    }
}
