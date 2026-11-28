using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CvAiAnalysisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_CvMatchRuns_ApplicationId_DocumentId_VacancyRevisionId_CvReviewVersionId_SourceSha256Hash_Id",
                table: "CvMatchRuns",
                columns: new[] { "ApplicationId", "DocumentId", "VacancyRevisionId", "CvReviewVersionId", "SourceSha256Hash", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ApplicationConsents_ApplicationId_Id",
                table: "ApplicationConsents",
                columns: new[] { "ApplicationId", "Id" });

            migrationBuilder.CreateTable(
                name: "CvAiAnalysisRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VacancyRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CvReviewVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CvMatchRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessingPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationConsentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModelDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InputSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    OutputSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    SourceSha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InputSha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvAiAnalysisRuns", x => x.Id);
                    table.CheckConstraint("CK_CvAiAnalysisRun_Attempts", "[AttemptCount] >= 0 AND [AttemptCount] <= 2");
                    table.CheckConstraint("CK_CvAiAnalysisRun_InputHash", "LEN([InputSha256Hash]) = 64 AND [InputSha256Hash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'");
                    table.CheckConstraint("CK_CvAiAnalysisRun_InputJson", "ISJSON([InputJson], OBJECT) = 1 AND DATALENGTH([InputJson]) <= 524288");
                    table.CheckConstraint("CK_CvAiAnalysisRun_Lifecycle", "(([Status] = 'Queued' AND [AttemptCount] = 0 AND [StartedAtUtc] IS NULL AND [CompletedAtUtc] IS NULL AND [ResultJson] IS NULL AND [FailureCode] IS NULL) OR ([Status] = 'Processing' AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NULL AND [ResultJson] IS NULL AND [FailureCode] IS NULL) OR ([Status] = 'Ready' AND [AttemptCount] BETWEEN 1 AND 2 AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [ResultJson] IS NOT NULL AND [FailureCode] IS NULL) OR ([Status] = 'Failed' AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [ResultJson] IS NULL AND [FailureCode] IS NOT NULL AND LEN(LTRIM(RTRIM([FailureCode]))) > 0))");
                    table.CheckConstraint("CK_CvAiAnalysisRun_Model", "LEN(LTRIM(RTRIM([Model]))) > 0");
                    table.CheckConstraint("CK_CvAiAnalysisRun_ModelDigest", "LEN([ModelDigest]) = 64 AND [ModelDigest] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'");
                    table.CheckConstraint("CK_CvAiAnalysisRun_PromptVersion", "LEN(LTRIM(RTRIM([PromptVersion]))) > 0");
                    table.CheckConstraint("CK_CvAiAnalysisRun_Provider", "LEN(LTRIM(RTRIM([Provider]))) > 0");
                    table.CheckConstraint("CK_CvAiAnalysisRun_ResultJson", "[ResultJson] IS NULL OR (ISJSON([ResultJson], OBJECT) = 1 AND DATALENGTH([ResultJson]) <= 524288)");
                    table.CheckConstraint("CK_CvAiAnalysisRun_SchemaVersions", "[InputSchemaVersion] > 0 AND [OutputSchemaVersion] > 0");
                    table.CheckConstraint("CK_CvAiAnalysisRun_SourceHash", "LEN([SourceSha256Hash]) = 64 AND [SourceSha256Hash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'");
                    table.CheckConstraint("CK_CvAiAnalysisRun_Status", "[Status] IN ('Queued','Processing','Ready','Failed')");
                    table.CheckConstraint("CK_CvAiAnalysisRun_TimestampOrder", "([StartedAtUtc] IS NULL OR [StartedAtUtc] >= [CreatedAtUtc]) AND ([CompletedAtUtc] IS NULL OR ([StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [StartedAtUtc]))");
                    table.CheckConstraint("CK_CvAiAnalysisRun_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_CvAiAnalysisRuns_ApplicationConsents_ApplicationId_ApplicationConsentId",
                        columns: x => new { x.ApplicationId, x.ApplicationConsentId },
                        principalTable: "ApplicationConsents",
                        principalColumns: new[] { "ApplicationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvAiAnalysisRuns_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvAiAnalysisRuns_CandidateApplications_ApplicationId_DocumentId",
                        columns: x => new { x.ApplicationId, x.DocumentId },
                        principalTable: "CandidateApplications",
                        principalColumns: new[] { "Id", "DocumentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvAiAnalysisRuns_CvDocuments_DocumentId_SourceSha256Hash",
                        columns: x => new { x.DocumentId, x.SourceSha256Hash },
                        principalTable: "CvDocuments",
                        principalColumns: new[] { "Id", "Sha256Hash" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvAiAnalysisRuns_CvMatchRuns_ApplicationId_DocumentId_VacancyRevisionId_CvReviewVersionId_SourceSha256Hash_CvMatchRunId",
                        columns: x => new { x.ApplicationId, x.DocumentId, x.VacancyRevisionId, x.CvReviewVersionId, x.SourceSha256Hash, x.CvMatchRunId },
                        principalTable: "CvMatchRuns",
                        principalColumns: new[] { "ApplicationId", "DocumentId", "VacancyRevisionId", "CvReviewVersionId", "SourceSha256Hash", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvAiAnalysisRuns_CvReviewVersions_ApplicationId_DocumentId_CvReviewVersionId_VacancyRevisionId",
                        columns: x => new { x.ApplicationId, x.DocumentId, x.CvReviewVersionId, x.VacancyRevisionId },
                        principalTable: "CvReviewVersions",
                        principalColumns: new[] { "ApplicationId", "DocumentId", "Id", "VacancyRevisionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvAiAnalysisRuns_ProcessingPolicies_ProcessingPolicyId",
                        column: x => x.ProcessingPolicyId,
                        principalTable: "ProcessingPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvAiAnalysisRuns_VacancyRevisions_VacancyRevisionId",
                        column: x => x.VacancyRevisionId,
                        principalTable: "VacancyRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_ApplicationConsentId",
                table: "CvAiAnalysisRuns",
                column: "ApplicationConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_ApplicationId_ApplicationConsentId",
                table: "CvAiAnalysisRuns",
                columns: new[] { "ApplicationId", "ApplicationConsentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_ApplicationId_DocumentId_CvReviewVersionId_VacancyRevisionId",
                table: "CvAiAnalysisRuns",
                columns: new[] { "ApplicationId", "DocumentId", "CvReviewVersionId", "VacancyRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_ApplicationId_DocumentId_VacancyRevisionId_CvReviewVersionId_SourceSha256Hash_CvMatchRunId",
                table: "CvAiAnalysisRuns",
                columns: new[] { "ApplicationId", "DocumentId", "VacancyRevisionId", "CvReviewVersionId", "SourceSha256Hash", "CvMatchRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_ApplicationId_Version",
                table: "CvAiAnalysisRuns",
                columns: new[] { "ApplicationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_CvMatchRunId",
                table: "CvAiAnalysisRuns",
                column: "CvMatchRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_CvReviewVersionId",
                table: "CvAiAnalysisRuns",
                column: "CvReviewVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_DocumentId_SourceSha256Hash",
                table: "CvAiAnalysisRuns",
                columns: new[] { "DocumentId", "SourceSha256Hash" });

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_ProcessingPolicyId",
                table: "CvAiAnalysisRuns",
                column: "ProcessingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_RequestedByUserId",
                table: "CvAiAnalysisRuns",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_Status_CreatedAtUtc",
                table: "CvAiAnalysisRuns",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CvAiAnalysisRuns_VacancyRevisionId",
                table: "CvAiAnalysisRuns",
                column: "VacancyRevisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CvAiAnalysisRuns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CvMatchRuns_ApplicationId_DocumentId_VacancyRevisionId_CvReviewVersionId_SourceSha256Hash_Id",
                table: "CvMatchRuns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ApplicationConsents_ApplicationId_Id",
                table: "ApplicationConsents");
        }
    }
}
