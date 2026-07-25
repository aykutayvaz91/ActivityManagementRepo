using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    // Talep kaynağı: hangi dış portaldan geldi.
    public enum RequestSource
    {
        SunucuKurulum = 0,   // psm.tdv.org — sunucu kurulum talepleri
        DisDestek = 1        // destek.cmit.com.tr — dış destek talepleri
    }

    // Talep yaşam döngüsü.
    public enum RequestStatus
    {
        Yeni = 0,          // portaldan yeni geldi, atanmadı
        Atandi = 1,        // bir uzmana atandı
        DevamEdiyor = 2,   // üzerinde çalışılıyor
        Beklemede = 3,     // bilgi/aksiyon bekliyor
        Cozuldu = 4,       // çözüldü (kapanış bekliyor)
        Kapandi = 5,       // kapandı
        Iptal = 6          // iptal
    }

    // Dış portallardan (psm.tdv.org / destek.cmit.com.tr) gelen, EFORLU İŞ olarak yönetilen talep.
    // Görev/Faaliyet gibi ActivityLog'a efor yazılır (ServiceRequestId). Kaynağa özel alanlar burada tutulur.
    public class ServiceRequest : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public RequestSource Source { get; set; }

        // Portaldaki talep numarası. (Source + ExternalRef) benzersiz kabul edilir → idempotent upsert.
        public string ExternalRef { get; set; }
        // Portaldaki talebe tıklanır bağlantı (varsa).
        public string ExternalUrl { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        // Talep eden (sistemimiz dışı bir kişi/müşteri olabilir).
        public string RequesterName { get; set; }
        public string RequesterEmail { get; set; }

        // Kaynağa özel serbest bilgi (sunucu adı / müşteri / ortam vb.).
        public string ExtraInfo { get; set; }

        public RequestStatus Status { get; set; } = RequestStatus.Yeni;

        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
        // Dinamik önem (1-10; 10 = en yüksek). Listeler bu ve SLA'ya göre sıralanır.
        public int PriorityScore { get; set; } = 5;

        public long? AssignedEmployeeId { get; set; }
        public virtual Employee AssignedEmployee { get; set; }

        public long? SecondaryEmployeeId { get; set; }
        public virtual Employee SecondaryEmployee { get; set; }

        public long? TeamId { get; set; }
        public virtual Team Team { get; set; }

        public long? CategoryId { get; set; }
        public virtual Category Category { get; set; }

        public long? SubCategoryId { get; set; }
        public virtual SubCategory SubCategory { get; set; }

        public long? ProjectId { get; set; }
        public virtual Project Project { get; set; }

        public DateTime? ReceivedDate { get; set; }  // portaldan geliş
        public DateTime? DueDate { get; set; }        // SLA / hedef tarih
        public DateTime? ResolvedDate { get; set; }
        public DateTime? ClosedDate { get; set; }

        public int CompletionPercentage { get; set; }

        public virtual ICollection<ActivityLog> Logs { get; set; } = new List<ActivityLog>();
    }
}
