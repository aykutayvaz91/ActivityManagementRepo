using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class TaskAttachment : CreationAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long TaskItemId { get; set; }
        public virtual TaskItem TaskItem { get; set; }

        // Bir yoruma bağlı ek (opsiyonel)
        public long? TaskCommentId { get; set; }
        public virtual TaskComment TaskComment { get; set; }

        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string ContentType { get; set; }
    }
}
