using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class V6_Integration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    InboundApiKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SyncEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AuthHeader = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AuthScheme = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Filter = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    InitialLookbackDays = table.Column<int>(type: "int", nullable: false),
                    LastSyncUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastResult = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSources_Source",
                table: "IntegrationSources",
                column: "Source",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationSettings");

            migrationBuilder.DropTable(
                name: "IntegrationSources");
        }
    }
}
