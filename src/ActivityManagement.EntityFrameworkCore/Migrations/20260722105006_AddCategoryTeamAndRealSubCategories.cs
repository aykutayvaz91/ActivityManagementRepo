using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryTeamAndRealSubCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TeamId",
                table: "Categories",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TeamId",
                table: "Categories",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Teams_TeamId",
                table: "Categories",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Mevcut 13 ana kategori şu anki tek takım olan "Infrastructure"a bağlanır.
            migrationBuilder.Sql(@"
UPDATE Categories SET TeamId = (SELECT TOP 1 Id FROM Teams WHERE Name = N'Infrastructure') WHERE TeamId IS NULL;
");

            // Kategoriler.txt'deki gerçek alt kategori listesiyle, önceki (uydurulan) alt kategorileri değiştir.
            migrationBuilder.Sql(@"
UPDATE TaskItems SET SubCategoryId = NULL WHERE SubCategoryId IN (SELECT Id FROM SubCategories);
DELETE FROM SubCategories;
");

            var map = new (string Category, string[] SubCategories)[]
            {
                ("Sunucu & Sanallaştırma", new[]
                {
                    "Sunucu Donanım Seviyesi & iDRAC", "Sunucu OS Seviyesi", "HCI-S2D", "Azure Local Migration",
                    "Kubernetes", "RDS (Remote Desktop Services)", "SCCM (VMM/SCOM)", "SCCM-ARC"
                }),
                ("Depolama & Yedekleme", new[] { "Storage Donanım Yönetimi", "Veeam", "FM 800" }),
                ("Ağ & Bağlantı", new[]
                {
                    "Switch-Omurga", "Switch Yedekliliği", "Topoloji ve Monitoring", "Palo Alto Firewall",
                    "ISE Cisco Identity Services Engine", "Sophos Firewall"
                }),
                ("Siber Güvenlik & Tehdit Yönetimi", new[]
                {
                    "Firewall Yetkilendirme", "Opensource Ürünler", "Microsoft Security Endpoint", "QRadar (SIEM)",
                    "Pentest", "Güvenlik prosedürleri oluşturma", "Otomasyon"
                }),
                ("Kimlik & Dizin Hizmetleri", new[] { "Active Directory", "DNS-DHCP", "ADFS-WAP", "Azure AD Connect", "EntraID" }),
                ("Varlık, Lisans & Sertifika", new[]
                {
                    "CA (Certificate Authority)", "KMS", "Sertifika Takibi ve Yüklenmesi",
                    "LMS & Otomasyon Projesi", "ITAM & Lifecycle"
                }),
                ("Dosya, Bulut & İşbirliği", new[] { "File Serverlar", "Azure", "Google Workspace", "IFS (ERP)", "Google Migration" }),
                ("İş Uygulamaları & Veritabanı", new[]
                {
                    "TDV Dynamics CRM", "TDV Sybase", "Matbaa Nebim (ERP)", "Teyas Mira (Eski ERP)",
                    "TDV 3CX", "TDV Milestone Kamera"
                }),
                ("Son Kullanıcı & Cihaz Yönetimi (Endpoint/MDM)", new[] { "Intune", "ScaleFusion" }),
                ("İzleme & Gözlemlenebilirlik", new[] { "PRTG", "GRAFANA", "ZABBIX" }),
                ("Eğitim", new[]
                {
                    "Kişisel Gelişim / Sertifikasyon", "Ekip İçi Eğitim & Bilgi Paylaşımı",
                    "Ürün / Vendor Eğitimi", "Dokümantasyon"
                }),
                ("Yenilik & Araştırma", new[]
                {
                    "Teknoloji Araştırması (PoC / Kavram Kanıtı)",
                    "Çözüm Değerlendirme, İyileştirme & Otomasyon", "Test çalışmaları"
                }),
                ("İdari & Operasyonel", new[]
                {
                    "Toplantı & Koordinasyon", "Raporlama & Planlama", "Tedarikçi / Vendor Yönetimi", "Satınalma"
                }),
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
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Teams_TeamId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_TeamId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Categories");
        }
    }
}
