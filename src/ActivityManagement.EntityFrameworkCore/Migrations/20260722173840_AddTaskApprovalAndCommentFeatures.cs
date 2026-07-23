using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskApprovalAndCommentFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "TaskItems",
                type: "int",
                nullable: false,
                defaultValue: 1); // Mevcut görevler Onaylandi kalsın

            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "TaskComments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TaskCommentId",
                table: "TaskAttachments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_TaskCommentId",
                table: "TaskAttachments",
                column: "TaskCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAttachments_TaskComments_TaskCommentId",
                table: "TaskAttachments",
                column: "TaskCommentId",
                principalTable: "TaskComments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAttachments_TaskComments_TaskCommentId",
                table: "TaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_TaskCommentId",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "TaskCommentId",
                table: "TaskAttachments");
        }
    }
}
