using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class SeedFixedMainCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Önceki migration'da (AddCategoryAndSubCategory) otomatik türetilmiş 12 kategori/alt kategori
            // hiçbir gerçek göreve bağlı değildi (üretim verisinde eski Category string alanı boştu).
            // Artık ana kategoriler dinamik değil, sabit 13 kalemden oluşuyor - eski geçici verileri temizleyip
            // yerine sabit listeyi koyuyoruz.
            migrationBuilder.Sql(@"
UPDATE TaskItems SET SubCategoryId = NULL WHERE SubCategoryId IN (SELECT Id FROM SubCategories);
DELETE FROM SubCategories;
DELETE FROM Categories;
");

            var fixedCategories = new[]
            {
                "Sunucu & Sanallaştırma",
                "Depolama & Yedekleme",
                "Ağ & Bağlantı",
                "Siber Güvenlik & Tehdit Yönetimi",
                "Kimlik & Dizin Hizmetleri",
                "Varlık, Lisans & Sertifika",
                "Dosya, Bulut & İşbirliği",
                "İş Uygulamaları & Veritabanı",
                "Son Kullanıcı & Cihaz Yönetimi (Endpoint/MDM)",
                "İzleme & Gözlemlenebilirlik",
                "Eğitim",
                "Yenilik & Araştırma",
                "İdari & Operasyonel",
            };

            foreach (var name in fixedCategories)
            {
                var escaped = name.Replace("'", "''");
                migrationBuilder.Sql($@"
INSERT INTO Categories (TenantId, Name, Description, ResponsibleEmployee1Id, ResponsibleEmployee2Id, IsActive, CreationTime, IsDeleted)
VALUES (1, N'{escaped}', NULL, NULL, NULL, 1, GETUTCDATE(), 0);
");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE TaskItems SET SubCategoryId = NULL WHERE SubCategoryId IN (SELECT Id FROM SubCategories);
DELETE FROM SubCategories;
DELETE FROM Categories;
");
        }
    }
}
