using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class RemoveOldGreets : Migration
    {
          /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            // Migrate existing greet configurations to MultiGreets
            migrationBuilder.Sql(@"
                INSERT INTO ""MultiGreets"" (""GuildId"", ""ChannelId"", ""Message"", ""DeleteTime"", ""WebhookUrl"", ""Disabled"", ""GreetBots"")
                SELECT
                    ""GuildId"",
                    ""GreetMessageChannelId"",
                    COALESCE(""ChannelGreetMessageText"", 'Welcome %user%'),
                    CASE
                        WHEN ""AutoDeleteGreetMessages"" = true THEN COALESCE(""AutoDeleteGreetMessagesTimer"", 1)
                        ELSE 0
                    END,
                    ""GreetHook"",
                    CASE
                        WHEN ""SendChannelGreetMessage"" = false THEN true
                        ELSE false
                    END,
                    false
                FROM ""GuildConfigs""
                WHERE ""GreetMessageChannelId"" != 0
                    AND ""SendChannelGreetMessage"" = true;
            ");

            // Drop the old columns
            migrationBuilder.DropColumn(
                name: "AutoDeleteGreetMessages",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "AutoDeleteGreetMessagesTimer",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "ChannelGreetMessageText",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "GreetHook",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "GreetMessageChannelId",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "SendChannelGreetMessage",
                table: "GuildConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the old columns
            migrationBuilder.AddColumn<bool>(
                name: "AutoDeleteGreetMessages",
                table: "GuildConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoDeleteGreetMessagesTimer",
                table: "GuildConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChannelGreetMessageText",
                table: "GuildConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GreetHook",
                table: "GuildConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GreetMessageChannelId",
                table: "GuildConfigs",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SendChannelGreetMessage",
                table: "GuildConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Migrate data back from MultiGreets to GuildConfigs
            migrationBuilder.Sql(@"
                UPDATE ""GuildConfigs"" gc
                SET
                    ""AutoDeleteGreetMessages"" = CASE WHEN mg.""DeleteTime"" > 0 THEN true ELSE false END,
                    ""AutoDeleteGreetMessagesTimer"" = mg.""DeleteTime"",
                    ""ChannelGreetMessageText"" = mg.""Message"",
                    ""GreetHook"" = mg.""WebhookUrl"",
                    ""GreetMessageChannelId"" = mg.""ChannelId"",
                    ""SendChannelGreetMessage"" = CASE WHEN mg.""Disabled"" = true THEN false ELSE true END
                FROM ""MultiGreets"" mg
                WHERE gc.""GuildId"" = mg.""GuildId"";
            ");

        }
    }
}
