using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondaryEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SecondaryEmployeeId",
                table: "TaskItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_SecondaryEmployeeId",
                table: "TaskItems",
                column: "SecondaryEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_Employees_SecondaryEmployeeId",
                table: "TaskItems",
                column: "SecondaryEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_Employees_SecondaryEmployeeId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_SecondaryEmployeeId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "SecondaryEmployeeId",
                table: "TaskItems");
        }
    }
}
