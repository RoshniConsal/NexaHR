using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CvMatchingAndSkillCatalogueSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_CvReviewVersions_ApplicationId_DocumentId_Id_VacancyRevisionId",
                table: "CvReviewVersions",
                columns: new[] { "ApplicationId", "DocumentId", "Id", "VacancyRevisionId" });

            migrationBuilder.CreateTable(
                name: "SkillCatalogueVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CatalogueJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentSha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillCatalogueVersions", x => x.Id);
                    table.CheckConstraint("CK_SkillCatalogueVersion_Hash", "LEN([ContentSha256Hash]) = 64");
                    table.CheckConstraint("CK_SkillCatalogueVersion_Json", "ISJSON([CatalogueJson], ARRAY) = 1");
                    table.CheckConstraint("CK_SkillCatalogueVersion_Parent", "([Version] = 1 AND [PreviousVersionId] IS NULL) OR ([Version] > 1 AND [PreviousVersionId] IS NOT NULL AND [PreviousVersionId] <> [Id])");
                    table.CheckConstraint("CK_SkillCatalogueVersion_ReviewReason", "LEN(LTRIM(RTRIM([ReviewReason]))) > 0");
                    table.CheckConstraint("CK_SkillCatalogueVersion_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_SkillCatalogueVersions_AspNetUsers_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SkillCatalogueVersions_SkillCatalogueVersions_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "SkillCatalogueVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CvMatchRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VacancyRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CvReviewVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillCatalogueVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RuleVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceSha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InputSha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MatchedWeight = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalWeight = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CriteriaSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExperienceAssessmentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvMatchRuns", x => x.Id);
                    table.CheckConstraint("CK_CvMatchRun_CriteriaJson", "ISJSON([CriteriaSnapshotJson], ARRAY) = 1");
                    table.CheckConstraint("CK_CvMatchRun_EvidenceJson", "ISJSON([EvidenceJson], ARRAY) = 1");
                    table.CheckConstraint("CK_CvMatchRun_ExperienceJson", "ISJSON([ExperienceAssessmentJson], OBJECT) = 1");
                    table.CheckConstraint("CK_CvMatchRun_InputHash", "LEN([InputSha256Hash]) = 64");
                    table.CheckConstraint("CK_CvMatchRun_RuleVersion", "LEN(LTRIM(RTRIM([RuleVersion]))) > 0");
                    table.CheckConstraint("CK_CvMatchRun_Score", "([TotalWeight] = 0 AND [Score] IS NULL) OR ([TotalWeight] > 0 AND [Score] IS NOT NULL AND [Score] >= 0 AND [Score] <= 100)");
                    table.CheckConstraint("CK_CvMatchRun_SourceHash", "LEN([SourceSha256Hash]) = 64");
                    table.CheckConstraint("CK_CvMatchRun_Version", "[Version] > 0");
                    table.CheckConstraint("CK_CvMatchRun_Weights", "[MatchedWeight] >= 0 AND [TotalWeight] >= 0 AND [MatchedWeight] <= [TotalWeight]");
                    table.ForeignKey(
                        name: "FK_CvMatchRuns_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvMatchRuns_CandidateApplications_ApplicationId_DocumentId",
                        columns: x => new { x.ApplicationId, x.DocumentId },
                        principalTable: "CandidateApplications",
                        principalColumns: new[] { "Id", "DocumentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvMatchRuns_CvDocuments_DocumentId_SourceSha256Hash",
                        columns: x => new { x.DocumentId, x.SourceSha256Hash },
                        principalTable: "CvDocuments",
                        principalColumns: new[] { "Id", "Sha256Hash" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvMatchRuns_CvReviewVersions_ApplicationId_DocumentId_CvReviewVersionId_VacancyRevisionId",
                        columns: x => new { x.ApplicationId, x.DocumentId, x.CvReviewVersionId, x.VacancyRevisionId },
                        principalTable: "CvReviewVersions",
                        principalColumns: new[] { "ApplicationId", "DocumentId", "Id", "VacancyRevisionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvMatchRuns_SkillCatalogueVersions_SkillCatalogueVersionId",
                        column: x => x.SkillCatalogueVersionId,
                        principalTable: "SkillCatalogueVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CvMatchRuns_VacancyRevisions_VacancyRevisionId",
                        column: x => x.VacancyRevisionId,
                        principalTable: "VacancyRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CvMatchRuns_ApplicationId_DocumentId_CvReviewVersionId_VacancyRevisionId",
                table: "CvMatchRuns",
                columns: new[] { "ApplicationId", "DocumentId", "CvReviewVersionId", "VacancyRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CvMatchRuns_ApplicationId_Version",
                table: "CvMatchRuns",
                columns: new[] { "ApplicationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CvMatchRuns_CreatedAtUtc",
                table: "CvMatchRuns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CvMatchRuns_CreatedByUserId",
                table: "CvMatchRuns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CvMatchRuns_CvReviewVersionId",
                table: "CvMatchRuns",
                column: "CvReviewVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CvMatchRuns_DocumentId_SourceSha256Hash",
                table: "CvMatchRuns",
                columns: new[] { "DocumentId", "SourceSha256Hash" });

            migrationBuilder.CreateIndex(
                name: "IX_CvMatchRuns_SkillCatalogueVersionId",
                table: "CvMatchRuns",
                column: "SkillCatalogueVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CvMatchRuns_VacancyRevisionId",
                table: "CvMatchRuns",
                column: "VacancyRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillCatalogueVersions_PreviousVersionId",
                table: "SkillCatalogueVersions",
                column: "PreviousVersionId",
                unique: true,
                filter: "[PreviousVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SkillCatalogueVersions_PublishedByUserId",
                table: "SkillCatalogueVersions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillCatalogueVersions_Version",
                table: "SkillCatalogueVersions",
                column: "Version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CvMatchRuns");

            migrationBuilder.DropTable(
                name: "SkillCatalogueVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CvReviewVersions_ApplicationId_DocumentId_Id_VacancyRevisionId",
                table: "CvReviewVersions");
        }
    }
}
