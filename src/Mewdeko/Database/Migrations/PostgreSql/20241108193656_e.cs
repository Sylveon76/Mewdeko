using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class E : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildConfigs_LogSettings_LogSettingId",
                table: "GuildConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_IgnoredLogChannels_LogSettings_LogSettingId",
                table: "IgnoredLogChannels");

            migrationBuilder.DropTable(
                name: "LogSettings");

            migrationBuilder.DropIndex(
                name: "IX_IgnoredLogChannels_LogSettingId",
                table: "IgnoredLogChannels");

            migrationBuilder.DropIndex(
                name: "IX_GuildConfigs_LogSettingId",
                table: "GuildConfigs");

            migrationBuilder.DropIndex(
                name: "IX_DiscordUser_UserId",
                table: "DiscordUser");

            migrationBuilder.DropColumn(
                name: "IsClubAdmin",
                table: "DiscordUser");

            migrationBuilder.CreateTable(
                name: "Embeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmbedName = table.Column<string>(type: "text", nullable: true),
                    JsonCode = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Embeds", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Embeds");

            migrationBuilder.AddColumn<bool>(
                name: "IsClubAdmin",
                table: "DiscordUser",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LogSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvatarUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelCreated = table.Column<long>(type: "bigint", nullable: false),
                    ChannelCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelDestroyed = table.Column<long>(type: "bigint", nullable: false),
                    ChannelDestroyedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelUpdated = table.Column<long>(type: "bigint", nullable: false),
                    ChannelUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EventCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    IsLogging = table.Column<long>(type: "bigint", nullable: false),
                    LogOtherId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogUserPresence = table.Column<long>(type: "bigint", nullable: false),
                    LogUserPresenceId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogVoicePresence = table.Column<long>(type: "bigint", nullable: false),
                    LogVoicePresenceId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogVoicePresenceTTSId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MessageDeleted = table.Column<long>(type: "bigint", nullable: false),
                    MessageDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MessageUpdated = table.Column<long>(type: "bigint", nullable: false),
                    MessageUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    NicknameUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ServerUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserBanned = table.Column<long>(type: "bigint", nullable: false),
                    UserBannedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserJoined = table.Column<long>(type: "bigint", nullable: false),
                    UserJoinedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserLeft = table.Column<long>(type: "bigint", nullable: false),
                    UserLeftId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserMutedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserPresenceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserRoleAddedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserRoleRemovedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserUnbanned = table.Column<long>(type: "bigint", nullable: false),
                    UserUnbannedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserUpdated = table.Column<long>(type: "bigint", nullable: false),
                    UserUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UsernameUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    VoicePresenceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredLogChannels_LogSettingId",
                table: "IgnoredLogChannels",
                column: "LogSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildConfigs_LogSettingId",
                table: "GuildConfigs",
                column: "LogSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordUser_UserId",
                table: "DiscordUser",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildConfigs_LogSettings_LogSettingId",
                table: "GuildConfigs",
                column: "LogSettingId",
                principalTable: "LogSettings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_IgnoredLogChannels_LogSettings_LogSettingId",
                table: "IgnoredLogChannels",
                column: "LogSettingId",
                principalTable: "LogSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
