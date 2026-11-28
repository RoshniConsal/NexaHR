using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CvReviewAndExtractionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_CvDocuments_Id_Sha256Hash",
                table: "CvDocuments",
                columns: new[] { "Id", "Sha256Hash" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CandidateApplications_Id_DocumentId",
                table: "CandidateApplications",
                columns: new[] { "Id", "DocumentId" });

            migrationBuilder.CreateTable(
                name: "CvExtractionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SourceSha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParserVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvExtractionRuns", x => x.Id);
                    table.UniqueConstraint("AK_CvExtractionRuns_ApplicationId_DocumentId_Id", x => new { x.ApplicationId, x.DocumentId, x.Id });
                    table.CheckConstraint("CK_CvExtractionRun_EvidenceJson", "[EvidenceJson] IS NULL OR ISJSON([EvidenceJson]) = 1");
                    table.CheckConstraint("CK_CvExtractionRun_Lifecycle", "(([Status] = 'Queued' AND [StartedAtUtc] IS NULL AND [CompletedAtUtc] IS NULL AND [ResultJson] IS NULL AND [EvidenceJson] IS NULL AND [FailureCode] IS NULL) OR ([Status] = 'Processing' AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NULL AND [ResultJson] IS NULL AND [EvidenceJson] IS NULL AND [FailureCode] IS NULL) OR ([Status] = 'ReadyForReview' AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [ResultJson] IS NOT NULL AND [FailureCode] IS NULL) OR ([Status] = 'Failed' AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [ResultJson] IS NULL AND [EvidenceJson] IS NULL AND [FailureCode] IS NOT NULL AND LEN(LTRIM(RTRIM([FailureCode]))) > 0))");
                    table.CheckConstraint("CK_CvExtractionRun_ParserVersion", "LEN(LTRIM(RTRIM([ParserVersion]))) > 0");
                    table.CheckConstraint("CK_CvExtractionRun_ResultJson", "[ResultJson] IS NULL OR ISJSON([ResultJson]) = 1");
                    table.CheckConstraint("CK_CvExtractionRun_SourceHash", "LEN([SourceSha256Hash]) = 64");
                    table.CheckConstraint("CK_CvExtractionRun_Status", "[Status] IN ('Queued','Processing','ReadyForReview','Failed')");
                    table.CheckConstraint("CK_CvExtractionRun_TimestampOrder", "([StartedAtUtc] IS NULL OR [StartedAtUtc] >= [CreatedAtUtc]) AND ([CompletedAtUtc] IS NULL OR ([StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [StartedAtUtc]))");
                    table.CheckConstraint("CK_CvExtractionRun_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_CvExtractionRuns_CandidateApplications_ApplicationId_DocumentId",
                        columns: x => new { x.ApplicationId, x.DocumentId },
                        principalTable: "CandidateApplications",
                        principalColumns: new[] { "Id", "DocumentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvExtractionRuns_CvDocuments_DocumentId_SourceSha256Hash",
                        columns: x => new { x.DocumentId, x.SourceSha256Hash },
                        principalTable: "CvDocuments",
                        principalColumns: new[] { "Id", "Sha256Hash" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CvReviewVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VacancyRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousReviewVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DuplicateResolutionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvReviewVersions", x => x.Id);
                    table.UniqueConstraint("AK_CvReviewVersions_ApplicationId_DocumentId_Id", x => new { x.ApplicationId, x.DocumentId, x.Id });
                    table.CheckConstraint("CK_CvReviewVersion_ChangedFieldsJson", "ISJSON([ChangedFieldsJson]) = 1");
                    table.CheckConstraint("CK_CvReviewVersion_Confirmation", "([Status] = 'Draft' AND [ConfirmedAtUtc] IS NULL) OR ([Status] = 'Confirmed' AND [ConfirmedAtUtc] IS NOT NULL AND [ConfirmedAtUtc] >= [CreatedAtUtc])");
                    table.CheckConstraint("CK_CvReviewVersion_DuplicateResolutionJson", "ISJSON([DuplicateResolutionJson]) = 1");
                    table.CheckConstraint("CK_CvReviewVersion_History", "([Version] = 1 AND [PreviousReviewVersionId] IS NULL) OR ([Version] > 1 AND [PreviousReviewVersionId] IS NOT NULL AND [PreviousReviewVersionId] <> [Id])");
                    table.CheckConstraint("CK_CvReviewVersion_Status", "[Status] IN ('Draft','Confirmed')");
                    table.CheckConstraint("CK_CvReviewVersion_ValuesJson", "ISJSON([ValuesJson]) = 1");
                    table.CheckConstraint("CK_CvReviewVersion_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_CvReviewVersions_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvReviewVersions_CandidateApplications_ApplicationId_DocumentId",
                        columns: x => new { x.ApplicationId, x.DocumentId },
                        principalTable: "CandidateApplications",
                        principalColumns: new[] { "Id", "DocumentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvReviewVersions_CvExtractionRuns_ApplicationId_DocumentId_ExtractionRunId",
                        columns: x => new { x.ApplicationId, x.DocumentId, x.ExtractionRunId },
                        principalTable: "CvExtractionRuns",
                        principalColumns: new[] { "ApplicationId", "DocumentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvReviewVersions_CvReviewVersions_ApplicationId_DocumentId_PreviousReviewVersionId",
                        columns: x => new { x.ApplicationId, x.DocumentId, x.PreviousReviewVersionId },
                        principalTable: "CvReviewVersions",
                        principalColumns: new[] { "ApplicationId", "DocumentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvReviewVersions_VacancyRevisions_VacancyRevisionId",
                        column: x => x.VacancyRevisionId,
                        principalTable: "VacancyRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CvExtractionRuns_ApplicationId_Version",
                table: "CvExtractionRuns",
                columns: new[] { "ApplicationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CvExtractionRuns_DocumentId_SourceSha256Hash",
                table: "CvExtractionRuns",
                columns: new[] { "DocumentId", "SourceSha256Hash" });

            migrationBuilder.CreateIndex(
                name: "IX_CvExtractionRuns_Status_CreatedAtUtc",
                table: "CvExtractionRuns",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CvReviewVersions_ApplicationId_DocumentId_ExtractionRunId",
                table: "CvReviewVersions",
                columns: new[] { "ApplicationId", "DocumentId", "ExtractionRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_CvReviewVersions_ApplicationId_DocumentId_PreviousReviewVersionId",
                table: "CvReviewVersions",
                columns: new[] { "ApplicationId", "DocumentId", "PreviousReviewVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CvReviewVersions_ApplicationId_Status_Version",
                table: "CvReviewVersions",
                columns: new[] { "ApplicationId", "Status", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_CvReviewVersions_ApplicationId_Version",
                table: "CvReviewVersions",
                columns: new[] { "ApplicationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CvReviewVersions_PreviousReviewVersionId",
                table: "CvReviewVersions",
                column: "PreviousReviewVersionId",
                unique: true,
                filter: "[PreviousReviewVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CvReviewVersions_ReviewerUserId",
                table: "CvReviewVersions",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CvReviewVersions_VacancyRevisionId",
                table: "CvReviewVersions",
                column: "VacancyRevisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CvReviewVersions");

            migrationBuilder.DropTable(
                name: "CvExtractionRuns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CvDocuments_Id_Sha256Hash",
                table: "CvDocuments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CandidateApplications_Id_DocumentId",
                table: "CandidateApplications");
        }
    }
}
