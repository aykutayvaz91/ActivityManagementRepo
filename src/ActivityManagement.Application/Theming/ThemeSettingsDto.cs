namespace ActivityManagement.Theming
{
    public class ThemeSettingsDto
    {
        public string PrimaryColor { get; set; } = "#0d6efd";
        public string LogoUrl { get; set; }
        public string BrandName { get; set; } = "Faaliyet Yönetim Sistemi";
        // Açıksa üst menüde sabit marka yerine giriş yapan kişinin takımının kısa adı gösterilir.
        public bool UseTeamNameAsBrand { get; set; }
        // Hesaplanmış marka (geçerli kullanıcıya göre): toggle açık + takım kısa adı varsa o, yoksa BrandName.
        public string EffectiveBrand { get; set; }
    }
}
