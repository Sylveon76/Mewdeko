using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class InstanceManagementStuffsAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BotUrl",
                table: "BotInstances",
                newName: "BotName");

            migrationBuilder.AddColumn<string>(
                name: "BotAvatar",
                table: "BotInstances",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "BotId",
                table: "BotInstances",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BotInstances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusUpdate",
                table: "BotInstances",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Port",
                table: "BotInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotAvatar",
                table: "BotInstances");

            migrationBuilder.DropColumn(
                name: "BotId",
                table: "BotInstances");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BotInstances");

            migrationBuilder.DropColumn(
                name: "LastStatusUpdate",
                table: "BotInstances");

            migrationBuilder.DropColumn(
                name: "Port",
                table: "BotInstances");

            migrationBuilder.RenameColumn(
                name: "BotName",
                table: "BotInstances",
                newName: "BotUrl");
        }
    }
}
