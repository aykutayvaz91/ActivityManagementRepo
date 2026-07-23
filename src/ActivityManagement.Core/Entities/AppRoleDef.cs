using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Dinamik rol tanımı. Sistem rolleri (Admin/TakımLideri/Uzman) IsSystem=true (silinemez).
    // Admin buraya ara/özel roller ekleyebilir; Employee.AppRole bu Name ile eşleşir.
    public class AppRoleDef : Entity<int>
    {
        public string Name { get; set; }         // claim değeri (ör. "TakımLideri", "Gözlemci")
        public string DisplayName { get; set; }   // görünen ad
        public bool IsSystem { get; set; }         // Admin/TakımLideri/Uzman => true
        public int SortOrder { get; set; }
    }

    // Rol × Sayfa erişimi (admin panelden yönetilir).
    public class RolePageAccess : Entity<int>
    {
        public string RoleName { get; set; }
        public string PageKey { get; set; }
        public bool Allowed { get; set; }
    }
}
