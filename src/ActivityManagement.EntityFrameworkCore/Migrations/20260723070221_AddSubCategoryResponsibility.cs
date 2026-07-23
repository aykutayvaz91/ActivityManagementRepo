using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSubCategoryResponsibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubCategoryResponsibilities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SubCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    ResponsibilityType = table.Column<int>(type: "int", nullable: false),
                    AssignedByTeamLeaderId = table.Column<long>(type: "bigint", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategoryResponsibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCategoryResponsibilities_Employees_AssignedByTeamLeaderId",
                        column: x => x.AssignedByTeamLeaderId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubCategoryResponsibilities_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubCategoryResponsibilities_SubCategories_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "SubCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubCategoryResponsibilities_AssignedByTeamLeaderId",
                table: "SubCategoryResponsibilities",
                column: "AssignedByTeamLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategoryResponsibilities_EmployeeId",
                table: "SubCategoryResponsibilities",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategoryResponsibilities_SubCategoryId_EmployeeId",
                table: "SubCategoryResponsibilities",
                columns: new[] { "SubCategoryId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubCategoryResponsibilities");
        }
    }
}
