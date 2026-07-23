using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryAndSubCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SubCategoryId",
                table: "TaskItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponsibleEmployee1Id = table.Column<long>(type: "bigint", nullable: true),
                    ResponsibleEmployee2Id = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Employees_ResponsibleEmployee1Id",
                        column: x => x.ResponsibleEmployee1Id,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Categories_Employees_ResponsibleEmployee2Id",
                        column: x => x.ResponsibleEmployee2Id,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_SubCategoryId",
                table: "TaskItems",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ResponsibleEmployee1Id",
                table: "Categories",
                column: "ResponsibleEmployee1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ResponsibleEmployee2Id",
                table: "Categories",
                column: "ResponsibleEmployee2Id");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CategoryId",
                table: "SubCategories",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_SubCategories_SubCategoryId",
                table: "TaskItems",
                column: "SubCategoryId",
                principalTable: "SubCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Mevcut TaskItems.Category (string, eski "Alt Başlık") değerlerini
            // yeni Category/SubCategory yapısına taşı: her benzersiz değer bir
            // Ana Kategori olur, altına placeholder "Genel" Alt Kategori eklenir
            // ve o değere sahip görevler bu alt kategoriye bağlanır. Sorumlu
            // atamaları boş bırakılır - admin panelinden sonradan atanacak.
            var legacyCategoryMap = new (string OldCategory, string NewCategoryName)[]
            {
                ("1.1. Sunucu ve Sanallaştırma Yönetimi", "Sunucu ve Sanallaştırma Yönetimi"),
                ("1.2. Storage Yönetimi", "Storage Yönetimi"),
                ("2.1. Yedekleme Stratejileri", "Yedekleme Stratejileri"),
                ("2.2. Replikasyon Stratejileri", "Replikasyon Stratejileri"),
                ("3.1. Kablolu Ağ Yönetimi", "Kablolu Ağ Yönetimi"),
                ("3.2. Kablosuz Ağ Yönetimi", "Kablosuz Ağ Yönetimi"),
                ("4.1. Güvenlik Duvarı", "Güvenlik Duvarı"),
                ("4.2. Yük Dengeleyici ve WAF", "Yük Dengeleyici ve WAF"),
                ("5.1. Azure IaaS/PaaS", "Azure IaaS/PaaS"),
                ("5.2. Microsoft 365", "Microsoft 365"),
                ("6.1. Son Kullanıcı Eğitimi ve Oryantasyon", "Son Kullanıcı Eğitimi ve Oryantasyon"),
                ("Uygulama / VM Takibi", "Uygulama / VM Takibi"),
            };

            foreach (var (oldCategory, newCategoryName) in legacyCategoryMap)
            {
                var escapedOld = oldCategory.Replace("'", "''");
                var escapedName = newCategoryName.Replace("'", "''");

                migrationBuilder.Sql($@"
DECLARE @CatId BIGINT, @SubCatId BIGINT;

INSERT INTO Categories (TenantId, Name, Description, ResponsibleEmployee1Id, ResponsibleEmployee2Id, IsActive, CreationTime, IsDeleted)
VALUES (1, N'{escapedName}', NULL, NULL, NULL, 1, GETUTCDATE(), 0);
SET @CatId = SCOPE_IDENTITY();

INSERT INTO SubCategories (TenantId, Name, Description, CategoryId, IsActive, CreationTime, IsDeleted)
VALUES (1, N'Genel', NULL, @CatId, 1, GETUTCDATE(), 0);
SET @SubCatId = SCOPE_IDENTITY();

UPDATE TaskItems SET SubCategoryId = @SubCatId WHERE Category = N'{escapedOld}';
");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_SubCategories_SubCategoryId",
                table: "TaskItems");

            migrationBuilder.DropTable(
                name: "SubCategories");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_SubCategoryId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "SubCategoryId",
                table: "TaskItems");
        }
    }
}
