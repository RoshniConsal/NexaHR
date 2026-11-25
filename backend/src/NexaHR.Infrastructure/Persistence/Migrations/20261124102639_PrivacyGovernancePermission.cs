using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivacyGovernancePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasPrivacyGovernanceAccess",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasPrivacyGovernanceAccess",
                table: "AspNetUsers");
        }
    }
}
