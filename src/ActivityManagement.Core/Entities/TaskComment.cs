using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class TaskComment : CreationAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long TaskItemId { get; set; }
        public virtual TaskItem TaskItem { get; set; }

        // Zengin metin (HTML). Ctrl+V ile yapıştırılan görseller base64 olarak gömülü gelebilir.
        public string Comment { get; set; }
        public string AuthorName { get; set; }

        // Dahili not: yalnızca Admin/TakımLideri görebilir
        public bool IsInternal { get; set; }

        public virtual System.Collections.Generic.ICollection<TaskAttachment> Attachments { get; set; }
            = new System.Collections.Generic.List<TaskAttachment>();
    }
}
