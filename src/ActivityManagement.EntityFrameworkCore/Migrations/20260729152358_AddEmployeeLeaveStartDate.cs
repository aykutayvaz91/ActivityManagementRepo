using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeLeaveStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LeaveStartDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaveStartDate",
                table: "Employees");
        }
    }
}
