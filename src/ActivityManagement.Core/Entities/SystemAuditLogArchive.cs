using System;
using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Denetim kaydı ARŞİVİ. Yıllık arşiv işi, geçmiş yıla ait SystemAuditLog kayıtlarını buraya taşır
    // ve sıcak tablodan siler (veri büyümesini sınırlar). Yalnız Admin "Arşiv Sorgu" ekranından okur.
    public class SystemAuditLogArchive : Entity<long>, IMayHaveTenant
    {
        public int? TenantId { get; set; }
        public long OriginalId { get; set; }        // sıcak tablodaki özgün Id (izlenebilirlik)
        public long? UserId { get; set; }
        public string UserName { get; set; }
        public DateTime ExecutionTime { get; set; }
        public string ClientIpAddress { get; set; }
        public string ActionType { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string OriginalValues { get; set; }
        public string NewValues { get; set; }
        public DateTime ArchivedAt { get; set; }     // arşive alınma anı
    }
}
