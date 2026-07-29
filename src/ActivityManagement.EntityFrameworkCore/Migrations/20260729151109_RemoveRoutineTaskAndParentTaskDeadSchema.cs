using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRoutineTaskAndParentTaskDeadSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_RoutineTasks_RoutineTaskId",
                table: "TaskItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_TaskItems_ParentTaskId",
                table: "TaskItems");

            migrationBuilder.DropTable(
                name: "RoutineTasks");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ParentTaskId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_RoutineTaskId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "IsRoutine",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "RoutineTaskId",
                table: "TaskItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRoutine",
                table: "TaskItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "ParentTaskId",
                table: "TaskItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RoutineTaskId",
                table: "TaskItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoutineTasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    RecurrenceDay = table.Column<int>(type: "int", nullable: false),
                    RecurrenceDays = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecurrenceType = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutineTasks_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ParentTaskId",
                table: "TaskItems",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_RoutineTaskId",
                table: "TaskItems",
                column: "RoutineTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutineTasks_EmployeeId",
                table: "RoutineTasks",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_RoutineTasks_RoutineTaskId",
                table: "TaskItems",
                column: "RoutineTaskId",
                principalTable: "RoutineTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_TaskItems_ParentTaskId",
                table: "TaskItems",
                column: "ParentTaskId",
                principalTable: "TaskItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
