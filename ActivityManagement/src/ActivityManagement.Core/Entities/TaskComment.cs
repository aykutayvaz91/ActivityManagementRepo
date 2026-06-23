using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class TaskComment : CreationAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long TaskItemId { get; set; }
        public virtual TaskItem TaskItem { get; set; }

        public string Comment { get; set; }
        public string AuthorName { get; set; }
    }
}
