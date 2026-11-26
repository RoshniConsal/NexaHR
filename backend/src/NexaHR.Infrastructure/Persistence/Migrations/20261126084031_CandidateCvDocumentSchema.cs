using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandidateCvDocumentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CvDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScanStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScanResultCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvDocuments", x => x.Id);
                    table.CheckConstraint("CK_CvDocument_ContentType", "[ContentType] IN ('application/pdf','application/vnd.openxmlformats-officedocument.wordprocessingml.document')");
                    table.CheckConstraint("CK_CvDocument_ScanMetadata", "([ScanStatus] = 'Pending' AND [ScannedAtUtc] IS NULL) OR ([ScanStatus] IN ('Clean','Infected','Failed') AND [ScannedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_CvDocument_ScanStatus", "[ScanStatus] IN ('Pending','Clean','Infected','Failed')");
                    table.CheckConstraint("CK_CvDocument_Sha256Hash", "LEN([Sha256Hash]) = 64");
                    table.CheckConstraint("CK_CvDocument_SizeBytes", "[SizeBytes] > 0 AND [SizeBytes] <= 5242880");
                    table.CheckConstraint("CK_CvDocument_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_CvDocuments_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CvDocuments_OwnerUserId_CreatedAtUtc",
                table: "CvDocuments",
                columns: new[] { "OwnerUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CvDocuments_OwnerUserId_Version",
                table: "CvDocuments",
                columns: new[] { "OwnerUserId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CvDocuments_ScanStatus_CreatedAtUtc",
                table: "CvDocuments",
                columns: new[] { "ScanStatus", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CvDocuments_StorageKey",
                table: "CvDocuments",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CvDocuments");
        }
    }
}
