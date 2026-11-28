using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandidateApplicationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_CvDocuments_OwnerUserId_Id",
                table: "CvDocuments",
                columns: new[] { "OwnerUserId", "Id" });

            migrationBuilder.CreateTable(
                name: "CandidateProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Skills = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Education = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_CandidateProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CandidateApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VacancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VacancyRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateApplications", x => x.Id);
                    table.CheckConstraint("CK_CandidateApplication_Stage", "[Stage] IN ('Applied','UnderReview','Shortlisted','Interview','OfferSent','OfferAccepted','Hired','Rejected','Withdrawn','OfferDeclined')");
                    table.ForeignKey(
                        name: "FK_CandidateApplications_CandidateProfiles_CandidateUserId",
                        column: x => x.CandidateUserId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateApplications_CvDocuments_CandidateUserId_DocumentId",
                        columns: x => new { x.CandidateUserId, x.DocumentId },
                        principalTable: "CvDocuments",
                        principalColumns: new[] { "OwnerUserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateApplications_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateApplications_VacancyRevisions_VacancyId_VacancyRevisionId",
                        columns: x => new { x.VacancyId, x.VacancyRevisionId },
                        principalTable: "VacancyRevisions",
                        principalColumns: new[] { "VacancyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TextVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConsentText = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    ProcessingPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Granted = table.Column<bool>(type: "bit", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationConsents", x => x.Id);
                    table.CheckConstraint("CK_ApplicationConsent_AiProvider", "[Purpose] <> 'AiProcessing' OR ([Provider] IS NOT NULL AND LEN(LTRIM(RTRIM([Provider]))) > 0 AND [ProcessingPolicyId] IS NOT NULL)");
                    table.CheckConstraint("CK_ApplicationConsent_Purpose", "[Purpose] IN ('Submission','AiProcessing')");
                    table.CheckConstraint("CK_ApplicationConsent_Submission", "[Purpose] <> 'Submission' OR ([Granted] = 1 AND [Provider] IS NULL)");
                    table.CheckConstraint("CK_ApplicationConsent_Text", "LEN(LTRIM(RTRIM([TextVersion]))) > 0 AND LEN(LTRIM(RTRIM([ConsentText]))) > 0");
                    table.ForeignKey(
                        name: "FK_ApplicationConsents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationConsents_CandidateApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "CandidateApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationConsents_ProcessingPolicies_ProcessingPolicyId",
                        column: x => x.ProcessingPolicyId,
                        principalTable: "ProcessingPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromStage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStageEvents", x => x.Id);
                    table.CheckConstraint("CK_ApplicationStageEvent_ChangedStage", "[FromStage] IS NULL OR [FromStage] <> [ToStage]");
                    table.CheckConstraint("CK_ApplicationStageEvent_FromStage", "[FromStage] IS NULL OR [FromStage] IN ('Applied','UnderReview','Shortlisted','Interview','OfferSent','OfferAccepted','Hired','Rejected','Withdrawn','OfferDeclined')");
                    table.CheckConstraint("CK_ApplicationStageEvent_InitialStage", "[FromStage] IS NOT NULL OR [ToStage] = 'Applied'");
                    table.CheckConstraint("CK_ApplicationStageEvent_Reason", "[ToStage] NOT IN ('Rejected','Withdrawn') OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0)");
                    table.CheckConstraint("CK_ApplicationStageEvent_ToStage", "[ToStage] IN ('Applied','UnderReview','Shortlisted','Interview','OfferSent','OfferAccepted','Hired','Rejected','Withdrawn','OfferDeclined')");
                    table.ForeignKey(
                        name: "FK_ApplicationStageEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationStageEvents_CandidateApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "CandidateApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationConsents_ActorUserId",
                table: "ApplicationConsents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationConsents_ApplicationId",
                table: "ApplicationConsents",
                column: "ApplicationId",
                unique: true,
                filter: "[Purpose] = 'Submission'");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationConsents_ApplicationId_Purpose_Provider_AtUtc",
                table: "ApplicationConsents",
                columns: new[] { "ApplicationId", "Purpose", "Provider", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationConsents_ProcessingPolicyId",
                table: "ApplicationConsents",
                column: "ProcessingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStageEvents_ActorUserId",
                table: "ApplicationStageEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStageEvents_ApplicationId",
                table: "ApplicationStageEvents",
                column: "ApplicationId",
                unique: true,
                filter: "[FromStage] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStageEvents_ApplicationId_AtUtc",
                table: "ApplicationStageEvents",
                columns: new[] { "ApplicationId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApplications_CandidateUserId_AppliedAtUtc",
                table: "CandidateApplications",
                columns: new[] { "CandidateUserId", "AppliedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApplications_CandidateUserId_DocumentId",
                table: "CandidateApplications",
                columns: new[] { "CandidateUserId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApplications_CandidateUserId_VacancyId",
                table: "CandidateApplications",
                columns: new[] { "CandidateUserId", "VacancyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApplications_VacancyId_Stage",
                table: "CandidateApplications",
                columns: new[] { "VacancyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApplications_VacancyId_VacancyRevisionId",
                table: "CandidateApplications",
                columns: new[] { "VacancyId", "VacancyRevisionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationConsents");

            migrationBuilder.DropTable(
                name: "ApplicationStageEvents");

            migrationBuilder.DropTable(
                name: "CandidateApplications");

            migrationBuilder.DropTable(
                name: "CandidateProfiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CvDocuments_OwnerUserId_Id",
                table: "CvDocuments");
        }
    }
}
