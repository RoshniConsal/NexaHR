using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VacancyWorkflowSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vacancies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacancies", x => x.Id);
                    table.CheckConstraint("CK_Vacancy_ClosureMetadata", "([Status] = 'Closed' AND [ClosedAtUtc] IS NOT NULL AND [ClosedByUserId] IS NOT NULL AND [ClosureReason] IS NOT NULL AND LEN(LTRIM(RTRIM([ClosureReason]))) > 0) OR ([Status] <> 'Closed' AND [ClosedAtUtc] IS NULL AND [ClosedByUserId] IS NULL AND [ClosureReason] IS NULL)");
                    table.CheckConstraint("CK_Vacancy_PublicationMetadata", "([PublishedRevisionId] IS NULL AND [PublishedAtUtc] IS NULL AND [PublishedByUserId] IS NULL) OR ([PublishedRevisionId] IS NOT NULL AND [PublishedAtUtc] IS NOT NULL AND [PublishedByUserId] IS NOT NULL)");
                    table.CheckConstraint("CK_Vacancy_PublishedStatus", "[Status] <> 'Published' OR [PublishedRevisionId] IS NOT NULL");
                    table.CheckConstraint("CK_Vacancy_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Published','Paused','Closed')");
                    table.ForeignKey(
                        name: "FK_Vacancies_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_AspNetUsers_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VacancyRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VacancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmploymentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Openings = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    MinimumExperienceYears = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ClosingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HiringReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyRevisions", x => x.Id);
                    table.UniqueConstraint("AK_VacancyRevisions_VacancyId_Id", x => new { x.VacancyId, x.Id });
                    table.CheckConstraint("CK_VacancyRevision_Experience", "[MinimumExperienceYears] IS NULL OR [MinimumExperienceYears] >= 0");
                    table.CheckConstraint("CK_VacancyRevision_Openings", "[Openings] > 0");
                    table.CheckConstraint("CK_VacancyRevision_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_VacancyRevision_Submission", "([SubmittedAtUtc] IS NULL AND [SubmittedByUserId] IS NULL) OR ([SubmittedAtUtc] IS NOT NULL AND [SubmittedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_VacancyRevisions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VacancyRevisions_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VacancyRevisions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VacancyRevisions_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VacancyCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VacancyRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyCriteria", x => x.Id);
                    table.CheckConstraint("CK_VacancyCriterion_Kind", "[Kind] IN ('RequiredSkill','PreferredSkill','InterviewCriterion','ReviewFlag')");
                    table.CheckConstraint("CK_VacancyCriterion_Weight", "[Weight] >= 0 AND ([Kind] <> 'RequiredSkill' OR [Weight] = 2) AND ([Kind] <> 'PreferredSkill' OR [Weight] = 1)");
                    table.ForeignKey(
                        name: "FK_VacancyCriteria_VacancyRevisions_VacancyRevisionId",
                        column: x => x.VacancyRevisionId,
                        principalTable: "VacancyRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VacancyDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VacancyRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyDecisions", x => x.Id);
                    table.CheckConstraint("CK_VacancyDecision_ApproverRole", "[ApproverRole] = 'CEO'");
                    table.CheckConstraint("CK_VacancyDecision_Decision", "[Decision] IN ('Approved','Rejected')");
                    table.CheckConstraint("CK_VacancyDecision_RejectionComment", "[Decision] <> 'Rejected' OR ([Comment] IS NOT NULL AND LEN(LTRIM(RTRIM([Comment]))) > 0)");
                    table.ForeignKey(
                        name: "FK_VacancyDecisions_AspNetUsers_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VacancyDecisions_VacancyRevisions_VacancyRevisionId",
                        column: x => x.VacancyRevisionId,
                        principalTable: "VacancyRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_ClosedByUserId",
                table: "Vacancies",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_CreatedByUserId",
                table: "Vacancies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_DepartmentId",
                table: "Vacancies",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Id_PublishedRevisionId",
                table: "Vacancies",
                columns: new[] { "Id", "PublishedRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_PublishedByUserId",
                table: "Vacancies",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Status",
                table: "Vacancies",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyCriteria_VacancyRevisionId_Kind_Label",
                table: "VacancyCriteria",
                columns: new[] { "VacancyRevisionId", "Kind", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VacancyDecisions_ApproverUserId",
                table: "VacancyDecisions",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyDecisions_VacancyRevisionId",
                table: "VacancyDecisions",
                column: "VacancyRevisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VacancyRevisions_CreatedByUserId",
                table: "VacancyRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyRevisions_DepartmentId",
                table: "VacancyRevisions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyRevisions_SubmittedByUserId",
                table: "VacancyRevisions",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyRevisions_VacancyId_Revision",
                table: "VacancyRevisions",
                columns: new[] { "VacancyId", "Revision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Vacancies_VacancyRevisions_Id_PublishedRevisionId",
                table: "Vacancies",
                columns: new[] { "Id", "PublishedRevisionId" },
                principalTable: "VacancyRevisions",
                principalColumns: new[] { "VacancyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vacancies_VacancyRevisions_Id_PublishedRevisionId",
                table: "Vacancies");

            migrationBuilder.DropTable(
                name: "VacancyCriteria");

            migrationBuilder.DropTable(
                name: "VacancyDecisions");

            migrationBuilder.DropTable(
                name: "VacancyRevisions");

            migrationBuilder.DropTable(
                name: "Vacancies");
        }
    }
}
