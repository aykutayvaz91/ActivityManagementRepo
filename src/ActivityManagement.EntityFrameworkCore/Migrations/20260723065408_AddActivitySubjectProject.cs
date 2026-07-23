using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddActivitySubjectProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                table: "ActivitySubjects",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySubjects_ProjectId",
                table: "ActivitySubjects",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivitySubjects_Projects_ProjectId",
                table: "ActivitySubjects",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivitySubjects_Projects_ProjectId",
                table: "ActivitySubjects");

            migrationBuilder.DropIndex(
                name: "IX_ActivitySubjects_ProjectId",
                table: "ActivitySubjects");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "ActivitySubjects");
        }
    }
}
