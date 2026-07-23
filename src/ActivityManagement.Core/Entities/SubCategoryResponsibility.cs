using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace ActivityManagement.Entities
{
    public enum ResponsibilityType
    {
        Primary = 0,  // Asıl Sorumlu
        Backup = 1    // Yedek Sorumlu
    }

    // Alt kategori bazlı sorumluluk matrisi: hangi personel hangi alt kategoride Asıl/Yedek sorumlu.
    public class SubCategoryResponsibility : CreationAuditedEntity<long>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public long SubCategoryId { get; set; }
        public virtual SubCategory SubCategory { get; set; }

        public long EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public ResponsibilityType ResponsibilityType { get; set; }

        public long? AssignedByTeamLeaderId { get; set; }
        public virtual Employee AssignedByTeamLeader { get; set; }
    }
}
