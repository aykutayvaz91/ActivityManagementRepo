using System.Collections.Generic;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public class Category : FullAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        // 1. Sorumlu
        public long? ResponsibleEmployee1Id { get; set; }
        public virtual Employee ResponsibleEmployee1 { get; set; }

        // 2. Sorumlu
        public long? ResponsibleEmployee2Id { get; set; }
        public virtual Employee ResponsibleEmployee2 { get; set; }

        public bool IsActive { get; set; } = true;

        // Ana kategori bir takıma aittir; alt kategorileri o takımın lideri yönetebilir (Admin hepsini yönetebilir)
        public long? TeamId { get; set; }
        public virtual Team Team { get; set; }

        public virtual ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
    }
}
