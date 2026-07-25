using Abp.Domain.Entities;

namespace ActivityManagement.Entities
{
    // Tek satırlık (Id=1) tema ayarı: ana renk (hex) ve şirket logosu yolu. Admin yönetir.
    public class ThemeSettings : Entity<int>
    {
        public string PrimaryColor { get; set; } = "#0d6efd"; // varsayılan Bootstrap mavisi
        public string LogoUrl { get; set; }
        public string BrandName { get; set; } = "Faaliyet Yönetim Sistemi";
        // Açıksa üst menüde sabit marka yerine, giriş yapan kişinin TAKIMININ kısa adı (ShortName) gösterilir.
        public bool UseTeamNameAsBrand { get; set; } = false;
    }
}
