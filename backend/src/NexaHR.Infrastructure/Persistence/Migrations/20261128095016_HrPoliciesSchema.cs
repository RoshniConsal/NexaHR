using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrPoliciesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HrPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraftTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DraftContent = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    DraftRequiresAcknowledgement = table.Column<bool>(type: "bit", nullable: false),
                    DraftVisibleToEmployees = table.Column<bool>(type: "bit", nullable: false),
                    DraftVisibleToHr = table.Column<bool>(type: "bit", nullable: false),
                    DraftVisibleToCeo = table.Column<bool>(type: "bit", nullable: false),
                    HasDraft = table.Column<bool>(type: "bit", nullable: false),
                    CurrentPublishedVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrPolicies_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrPolicies_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HrPolicyVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: false),
                    RequiresAcknowledgement = table.Column<bool>(type: "bit", nullable: false),
                    VisibleToEmployees = table.Column<bool>(type: "bit", nullable: false),
                    VisibleToHr = table.Column<bool>(type: "bit", nullable: false),
                    VisibleToCeo = table.Column<bool>(type: "bit", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPolicyVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrPolicyVersions_AspNetUsers_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrPolicyVersions_HrPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "HrPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PolicyAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgements_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgements_HrPolicyVersions_PolicyVersionId",
                        column: x => x.PolicyVersionId,
                        principalTable: "HrPolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrPolicies_CreatedByUserId",
                table: "HrPolicies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPolicies_CurrentPublishedVersion",
                table: "HrPolicies",
                column: "CurrentPublishedVersion");

            migrationBuilder.CreateIndex(
                name: "IX_HrPolicies_UpdatedByUserId",
                table: "HrPolicies",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPolicyVersions_PolicyId_Version",
                table: "HrPolicyVersions",
                columns: new[] { "PolicyId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrPolicyVersions_PublishedByUserId",
                table: "HrPolicyVersions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgements_PolicyVersionId_UserId",
                table: "PolicyAcknowledgements",
                columns: new[] { "PolicyVersionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgements_UserId_AcknowledgedAtUtc",
                table: "PolicyAcknowledgements",
                columns: new[] { "UserId", "AcknowledgedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyAcknowledgements");

            migrationBuilder.DropTable(
                name: "HrPolicyVersions");

            migrationBuilder.DropTable(
                name: "HrPolicies");
        }
    }
}
