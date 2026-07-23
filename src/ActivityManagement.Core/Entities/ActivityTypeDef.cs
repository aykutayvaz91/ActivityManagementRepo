using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    // Yönetici tarafından yönetilen dinamik Faaliyet Tipi listesi (Destek, Bakım, Geliştirme, Toplantı, İnceleme...).
    // ActivityLog.ActivityType (string) bu listeden seçilen ad ile doldurulur. (Enum ActivityType ayrı, görevler için legacy.)
    public class ActivityTypeDef : FullAuditedEntity<int>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public string Name { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
