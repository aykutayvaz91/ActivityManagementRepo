using System;
using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public enum TaskStatus
    {
        Beklemede = 0,
        DevamEdiyor = 1,
        Tamamlandi = 2,
        Iptal = 3,
        Ertelendi = 4,
        // Arşiv: tamamlanmış görev, tamamlandığı AY geçtikten sonra otomatik "Kapatıldı"ya çekilir
        // (aylık rapor çekildikten sonra). Rapor/geçmişte "tamamlanmış iş" sayılır; aktif ilerleme %'lerinde hesaba KATILMAZ.
        Kapatildi = 5
    }

    public enum TaskPriority
    {
        Dusuk = 0,
        Normal = 1,
        Yuksek = 2,
        Kritik = 3
    }

    // Öz görev onay durumu: Uzman kendi açtığı görev "Beklemede" gelir, Takım Lideri onaylayınca resmileşir.
    public enum TaskApprovalStatus
    {
        Beklemede = 0,
        Onaylandi = 1,
        Reddedildi = 2
    }

    public enum ActivityType
    {
        Bakim = 0,
        Gelistirme = 1,
        Kurulum = 2,
        Destek = 3,
        Test = 4,
        Dokumantasyon = 5,
        Egitim = 6,
        Analiz = 7,
        Proje = 8,
        Diger = 9
    }

    // ActivityType → Türkçe etiket (tek kaynak; Reports + Tasks buradan kullanır). Sınır-kontrollü.
    public static class ActivityTypeLabels
    {
        private static readonly string[] Labels =
            { "Bakım", "Geliştirme", "Kurulum", "Destek", "Test", "Dokümantasyon", "Eğitim", "Analiz", "Proje", "Diğer" };
        public static string Of(int idx) => idx >= 0 && idx < Labels.Length ? Labels[idx] : "Diğer";
        public static string Of(ActivityType t) => Of((int)t);
    }

    public class TaskItem : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        // Alt kategori (xlsx'teki Alt Başlık, örn. "1.1. Sunucu ve Sanallaştırma Yönetimi") - legacy, artık UI'da kullanılmıyor
        public string Category { get; set; }

        public long? SubCategoryId { get; set; }
        public virtual SubCategory SubCategory { get; set; }

        public long? TeamId { get; set; }
        public virtual Team Team { get; set; }

        public long? ProjectId { get; set; }
        public virtual Project Project { get; set; }

        public long? AssignedEmployeeId { get; set; }
        public virtual Employee AssignedEmployee { get; set; }

        // 2. Sorumlu (yedek)
        public long? SecondaryEmployeeId { get; set; }
        public virtual Employee SecondaryEmployee { get; set; }

        public long? AssignedByEmployeeId { get; set; }
        public virtual Employee AssignedByEmployee { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public TaskStatus Status { get; set; } = TaskStatus.Beklemede;
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;

        // Dinamik önem derecesi (1-10; 10 = en yüksek). Listeler/pano/takvim buna göre büyükten küçüğe sıralanır.
        public int PriorityScore { get; set; } = 5;

        // Öz görev onay mekanizması (varsayılan Onaylandi; Uzman'ın açtığı görevler Beklemede başlar)
        public TaskApprovalStatus ApprovalStatus { get; set; } = TaskApprovalStatus.Onaylandi;

        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }

        public int CompletionPercentage { get; set; }

        // Görev grubu (üst görevler için: "Sistem Birimi", "Network Birimi", "Ortak")
        public string GroupName { get; set; }

        // Faaliyet tipi (raporlama kırılımı)
        public ActivityType? ActivityType { get; set; }

        public virtual ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public virtual ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
        public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    }
}
