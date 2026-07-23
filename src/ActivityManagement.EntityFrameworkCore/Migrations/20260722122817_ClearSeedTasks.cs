using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class ClearSeedTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tüm dummy seed görevlerini temizle (temiz başlangıç).
            // Self-referencing FK (ParentTaskId, Restrict) nedeniyle önce üst-görev bağını çöz.
            // TaskComments/TaskAttachments -> Cascade, ActivityLogs.TaskItemId -> SetNull (DB otomatik).
            migrationBuilder.Sql("UPDATE TaskItems SET ParentTaskId = NULL;");
            migrationBuilder.Sql("DELETE FROM TaskItems;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Silinen tohum verisi geri alınamaz.
        }
    }
}
