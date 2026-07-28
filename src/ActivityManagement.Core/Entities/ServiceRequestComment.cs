using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    // Talebin PORTAL yorumlarının aynası (salt-okunur). Portalın yorum id'siyle (ExternalCommentId) dedup edilir.
    public class ServiceRequestComment : CreationAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long ServiceRequestId { get; set; }
        public virtual ServiceRequest ServiceRequest { get; set; }

        // Portaldaki yorumun kararlı kimliği (tekrar çekince kopyalanmaması için).
        public string ExternalCommentId { get; set; }

        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
        public DateTime? CommentDate { get; set; }
        public string Body { get; set; }          // düz metin veya HTML (görünümde sanitize edilir)
        public bool IsInternal { get; set; }
    }
}
