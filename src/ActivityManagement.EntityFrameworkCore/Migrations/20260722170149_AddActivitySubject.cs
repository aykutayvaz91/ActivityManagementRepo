using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddActivitySubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ActivitySubjectId",
                table: "ActivityLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivitySubjects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    SubCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByLeaderId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    TeamId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ActivitySubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivitySubjects_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivitySubjects_Employees_AssignedEmployeeId",
                        column: x => x.AssignedEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivitySubjects_Employees_CreatedByLeaderId",
                        column: x => x.CreatedByLeaderId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivitySubjects_SubCategories_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "SubCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivitySubjects_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ActivitySubjectId",
                table: "ActivityLogs",
                column: "ActivitySubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySubjects_AssignedEmployeeId",
                table: "ActivitySubjects",
                column: "AssignedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySubjects_CategoryId",
                table: "ActivitySubjects",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySubjects_CreatedByLeaderId",
                table: "ActivitySubjects",
                column: "CreatedByLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySubjects_SubCategoryId",
                table: "ActivitySubjects",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySubjects_TeamId",
                table: "ActivitySubjects",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_ActivitySubjects_ActivitySubjectId",
                table: "ActivityLogs",
                column: "ActivitySubjectId",
                principalTable: "ActivitySubjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_ActivitySubjects_ActivitySubjectId",
                table: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "ActivitySubjects");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_ActivitySubjectId",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "ActivitySubjectId",
                table: "ActivityLogs");
        }
    }
}
