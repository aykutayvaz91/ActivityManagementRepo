using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class Responsibility : AuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public int OrderNo { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
