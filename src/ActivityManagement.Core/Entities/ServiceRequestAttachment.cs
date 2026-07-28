using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    // Talebin PORTAL dosya eklerinin aynası (salt-okunur). Portalın dosya id'siyle (ExternalAttachmentId) dedup edilir.
    public class ServiceRequestAttachment : CreationAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long ServiceRequestId { get; set; }
        public virtual ServiceRequest ServiceRequest { get; set; }

        public string ExternalAttachmentId { get; set; }

        public string FileName { get; set; }
        public string Url { get; set; }           // portaldaki indirme adresi (aynı API anahtarıyla)
        public long SizeBytes { get; set; }
        public string ContentType { get; set; }
        public DateTime? UploadedAt { get; set; }
    }
}
