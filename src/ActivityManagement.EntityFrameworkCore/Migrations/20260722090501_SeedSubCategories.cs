using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class SeedSubCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 13 sabit ana kategorinin altına anlamlı, başlangıç alt kategorileri ekler
            // (boş bırakmak yerine) - Admin/TakımLideri sonradan ekleyip düzenleyebilir.
            var map = new (string Category, string[] SubCategories)[]
            {
                ("Sunucu & Sanallaştırma", new[] { "Sunucu OS Seviyesi", "Hypervisor Yönetimi", "Sanal Makine Yönetimi", "Kapasite Planlama" }),
                ("Depolama & Yedekleme", new[] { "Disk/SAN Yönetimi", "Yedekleme Stratejileri", "Felaket Kurtarma", "Veri Arşivleme" }),
                ("Ağ & Bağlantı", new[] { "Kablolu Ağ Yönetimi", "Kablosuz Ağ Yönetimi", "VPN Yönetimi", "IP Adresleme" }),
                ("Siber Güvenlik & Tehdit Yönetimi", new[] { "Güvenlik Duvarı", "Antivirüs / EDR", "Zafiyet Yönetimi", "Olay Müdahale (Incident Response)" }),
                ("Kimlik & Dizin Hizmetleri", new[] { "Active Directory", "Kimlik Doğrulama (SSO/MFA)", "Erişim Yönetimi (IAM)", "Sertifika Otoritesi" }),
                ("Varlık, Lisans & Sertifika", new[] { "Donanım Envanteri", "Yazılım Lisansları", "SSL Sertifikaları", "Sözleşme Takibi" }),
                ("Dosya, Bulut & İşbirliği", new[] { "Dosya Sunucuları", "Bulut Depolama (OneDrive/SharePoint)", "Ekip İşbirliği Araçları", "E-posta Sistemleri" }),
                ("İş Uygulamaları & Veritabanı", new[] { "ERP/CRM Uygulamaları", "Veritabanı Yönetimi", "Uygulama Entegrasyonları", "Performans İzleme" }),
                ("Son Kullanıcı & Cihaz Yönetimi (Endpoint/MDM)", new[] { "Bilgisayar Kurulumu", "Mobil Cihaz Yönetimi (MDM)", "Yazıcı / Çevre Birimleri", "Kullanıcı Destek Talepleri" }),
                ("İzleme & Gözlemlenebilirlik", new[] { "Sistem İzleme", "Log Yönetimi", "Alarm / Bildirim Yönetimi", "Kapasite Raporlama" }),
                ("Eğitim", new[] { "Son Kullanıcı Eğitimi", "Teknik Eğitimler", "Oryantasyon", "Güvenlik Farkındalığı" }),
                ("Yenilik & Araştırma", new[] { "Teknoloji Araştırma", "Proof of Concept (PoC)", "Süreç İyileştirme", "Pilot Projeler" }),
                ("İdari & Operasyonel", new[] { "Bütçe / Satın Alma", "Tedarikçi Yönetimi", "Dokümantasyon", "Raporlama" }),
            };

            foreach (var (categoryName, subCategories) in map)
            {
                var escapedCategory = categoryName.Replace("'", "''");
                foreach (var subName in subCategories)
                {
                    var escapedSub = subName.Replace("'", "''");
                    migrationBuilder.Sql($@"
DECLARE @CatId BIGINT;
SELECT @CatId = Id FROM Categories WHERE Name = N'{escapedCategory}';
IF @CatId IS NOT NULL
BEGIN
    INSERT INTO SubCategories (TenantId, Name, Description, CategoryId, IsActive, CreationTime, IsDeleted)
    VALUES (1, N'{escapedSub}', NULL, @CatId, 1, GETUTCDATE(), 0);
END
");
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bu migration ile eklenen alt kategorileri (Genel dışındakileri) geri almak riskli
            // (admin tarafından o zamana kadar elle eklenmiş/silinmiş olabilir) - Down boş bırakıldı.
        }
    }
}
