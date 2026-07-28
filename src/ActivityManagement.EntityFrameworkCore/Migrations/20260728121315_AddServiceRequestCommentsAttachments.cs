using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityManagement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestCommentsAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_Source_ExternalRef",
                table: "ServiceRequests");

            migrationBuilder.CreateTable(
                name: "ServiceRequestAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ServiceRequestId = table.Column<long>(type: "bigint", nullable: false),
                    ExternalAttachmentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequestAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceRequestAttachments_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequestComments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ServiceRequestId = table.Column<long>(type: "bigint", nullable: false),
                    ExternalCommentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuthorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CommentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequestComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceRequestComments_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_Source_ExternalRef",
                table: "ServiceRequests",
                columns: new[] { "Source", "ExternalRef" },
                unique: true,
                filter: "[ExternalRef] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequestAttachments_ServiceRequestId_ExternalAttachmentId",
                table: "ServiceRequestAttachments",
                columns: new[] { "ServiceRequestId", "ExternalAttachmentId" },
                unique: true,
                filter: "[ExternalAttachmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequestComments_ServiceRequestId_ExternalCommentId",
                table: "ServiceRequestComments",
                columns: new[] { "ServiceRequestId", "ExternalCommentId" },
                unique: true,
                filter: "[ExternalCommentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceRequestAttachments");

            migrationBuilder.DropTable(
                name: "ServiceRequestComments");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_Source_ExternalRef",
                table: "ServiceRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_Source_ExternalRef",
                table: "ServiceRequests",
                columns: new[] { "Source", "ExternalRef" });
        }
    }
}
