using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public enum NotificationType
    {
        GorevAtandi = 0,
        TalepAtandi = 1,
        SlaYaklasti = 2,
        DurumDegisti = 3,
        YorumEklendi = 4,
        FaaliyetAtandi = 5,
        Mesaj = 7,        // kişiden üst yöneticiye istek/mesaj
        Genel = 9
    }

    // Kişiye özel in-app bildirim. Polling ile çekilir; okundu/okunmadı takip edilir.
    public class Notification : CreationAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        // Bildirimin gideceği personel.
        public long RecipientEmployeeId { get; set; }

        public NotificationType Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Link { get; set; }      // tıklanınca gidilecek url
        public string Icon { get; set; }      // fontawesome ikon sınıfı (ör. fa-clipboard-list)
        public string Severity { get; set; }  // bootstrap renk: info/success/warning/danger

        public bool IsRead { get; set; }
    }
}
