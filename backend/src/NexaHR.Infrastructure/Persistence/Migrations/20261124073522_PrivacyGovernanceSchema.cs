using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivacyGovernanceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalHolds_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalHolds_AspNetUsers_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalHolds_AspNetUsers_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalHolds_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CandidateRetentionDays = table.Column<int>(type: "int", nullable: false),
                    EmploymentRetentionDays = table.Column<int>(type: "int", nullable: false),
                    AuditRetentionDays = table.Column<int>(type: "int", nullable: false),
                    CandidateRetentionAction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmploymentRetentionAction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AuditRetentionAction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProcessingPurpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DataCategories = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderRetention = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProviderTrainingAllowed = table.Column<bool>(type: "bit", nullable: false),
                    ProviderAccessPolicy = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ProcessingLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LegalHoldPolicy = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApprovalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingPolicies", x => x.Id);
                    table.CheckConstraint("CK_ProcessingPolicy_Actions", "[CandidateRetentionAction] IN ('Delete','Anonymise') AND [EmploymentRetentionAction] IN ('Delete','Anonymise') AND [AuditRetentionAction] IN ('Delete','Anonymise')");
                    table.CheckConstraint("CK_ProcessingPolicy_RetentionDays", "[CandidateRetentionDays] > 0 AND [EmploymentRetentionDays] > 0 AND [AuditRetentionDays] > 0");
                    table.CheckConstraint("CK_ProcessingPolicy_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_ProcessingPolicies_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessingPolicies_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalHoldEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalHoldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalHoldEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalHoldEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalHoldEvents_LegalHolds_LegalHoldId",
                        column: x => x.LegalHoldId,
                        principalTable: "LegalHolds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetentionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessingPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AuthorizationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaxRecords = table.Column<int>(type: "int", nullable: false),
                    EligibleCount = table.Column<int>(type: "int", nullable: false),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    HeldCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    CutoffUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionRuns", x => x.Id);
                    table.CheckConstraint("CK_RetentionRun_Action", "[Action] IN ('Delete','Anonymise')");
                    table.CheckConstraint("CK_RetentionRun_Counts", "[EligibleCount] >= 0 AND [ProcessedCount] >= 0 AND [HeldCount] >= 0 AND [FailedCount] >= 0 AND [ProcessedCount] <= [MaxRecords]");
                    table.CheckConstraint("CK_RetentionRun_MaxRecords", "[MaxRecords] > 0 AND [MaxRecords] <= 1000");
                    table.CheckConstraint("CK_RetentionRun_Status", "[Status] IN ('Pending','Running','Completed','Failed','Blocked')");
                    table.ForeignKey(
                        name: "FK_RetentionRuns_AspNetUsers_AuthorizedByUserId",
                        column: x => x.AuthorizedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetentionRuns_ProcessingPolicies_ProcessingPolicyId",
                        column: x => x.ProcessingPolicyId,
                        principalTable: "ProcessingPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalHoldEvents_ActorUserId",
                table: "LegalHoldEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHoldEvents_LegalHoldId",
                table: "LegalHoldEvents",
                column: "LegalHoldId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHoldEvents_SubjectId_CreatedAtUtc",
                table: "LegalHoldEvents",
                columns: new[] { "SubjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_CreatedByUserId",
                table: "LegalHolds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_IsActive",
                table: "LegalHolds",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_ReleasedByUserId",
                table: "LegalHolds",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_SubjectId",
                table: "LegalHolds",
                column: "SubjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_UpdatedByUserId",
                table: "LegalHolds",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingPolicies_ApprovedByUserId",
                table: "ProcessingPolicies",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingPolicies_RecordedByUserId",
                table: "ProcessingPolicies",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingPolicies_Version",
                table: "ProcessingPolicies",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRuns_AuthorizedByUserId",
                table: "RetentionRuns",
                column: "AuthorizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRuns_ProcessingPolicyId",
                table: "RetentionRuns",
                column: "ProcessingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRuns_Status_CreatedAtUtc",
                table: "RetentionRuns",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalHoldEvents");

            migrationBuilder.DropTable(
                name: "RetentionRuns");

            migrationBuilder.DropTable(
                name: "LegalHolds");

            migrationBuilder.DropTable(
                name: "ProcessingPolicies");
        }
    }
}
