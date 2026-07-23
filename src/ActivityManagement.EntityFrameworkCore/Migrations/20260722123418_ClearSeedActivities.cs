using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class ClearSeedActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tüm dummy faaliyet (ActivityLog) kayıtlarını temizle (temiz başlangıç).
            migrationBuilder.Sql("DELETE FROM ActivityLogs;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Silinen tohum verisi geri alınamaz.
        }
    }
}
