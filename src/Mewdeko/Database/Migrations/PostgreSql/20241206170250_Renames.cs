using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class Renames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "roles",
                table: "MutedUserId",
                newName: "Roles");

            migrationBuilder.RenameColumn(
                name: "LogVoicePresenceTTSId",
                table: "LoggingV2",
                newName: "LogVoicePresenceTtsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Roles",
                table: "MutedUserId",
                newName: "roles");

            migrationBuilder.RenameColumn(
                name: "LogVoicePresenceTtsId",
                table: "LoggingV2",
                newName: "LogVoicePresenceTTSId");
        }
    }
}
