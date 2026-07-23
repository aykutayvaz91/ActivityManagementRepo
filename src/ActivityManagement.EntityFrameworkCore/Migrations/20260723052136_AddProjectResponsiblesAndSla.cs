using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectResponsiblesAndSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PrimaryResponsibleId",
                table: "Projects",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SecondaryResponsibleId",
                table: "Projects",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaTargetDate",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_PrimaryResponsibleId",
                table: "Projects",
                column: "PrimaryResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_SecondaryResponsibleId",
                table: "Projects",
                column: "SecondaryResponsibleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Employees_PrimaryResponsibleId",
                table: "Projects",
                column: "PrimaryResponsibleId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Employees_SecondaryResponsibleId",
                table: "Projects",
                column: "SecondaryResponsibleId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Employees_PrimaryResponsibleId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Employees_SecondaryResponsibleId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_PrimaryResponsibleId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_SecondaryResponsibleId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PrimaryResponsibleId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SecondaryResponsibleId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SlaTargetDate",
                table: "Projects");
        }
    }
}
