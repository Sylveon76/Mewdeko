using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class CustomVoiceChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomVoiceChannel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OwnerId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastActive = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    KeepAlive = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedUsersJson = table.Column<string>(type: "text", nullable: true),
                    DeniedUsersJson = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomVoiceChannel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomVoiceConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HubVoiceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelCategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DefaultNameFormat = table.Column<string>(type: "text", nullable: false),
                    DefaultUserLimit = table.Column<int>(type: "integer", nullable: false),
                    DefaultBitrate = table.Column<int>(type: "integer", nullable: false),
                    DeleteWhenEmpty = table.Column<bool>(type: "boolean", nullable: false),
                    EmptyChannelTimeout = table.Column<int>(type: "integer", nullable: false),
                    AllowMultipleChannels = table.Column<bool>(type: "boolean", nullable: false),
                    AllowNameCustomization = table.Column<bool>(type: "boolean", nullable: false),
                    AllowUserLimitCustomization = table.Column<bool>(type: "boolean", nullable: false),
                    AllowBitrateCustomization = table.Column<bool>(type: "boolean", nullable: false),
                    AllowLocking = table.Column<bool>(type: "boolean", nullable: false),
                    AllowUserManagement = table.Column<bool>(type: "boolean", nullable: false),
                    MaxUserLimit = table.Column<int>(type: "integer", nullable: false),
                    MaxBitrate = table.Column<int>(type: "integer", nullable: false),
                    PersistUserPreferences = table.Column<bool>(type: "boolean", nullable: false),
                    AutoPermission = table.Column<bool>(type: "boolean", nullable: false),
                    CustomVoiceAdminRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomVoiceConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserVoicePreference",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    NameFormat = table.Column<string>(type: "text", nullable: true),
                    UserLimit = table.Column<int>(type: "integer", nullable: true),
                    Bitrate = table.Column<int>(type: "integer", nullable: true),
                    PreferLocked = table.Column<bool>(type: "boolean", nullable: true),
                    KeepAlive = table.Column<bool>(type: "boolean", nullable: true),
                    WhitelistJson = table.Column<string>(type: "text", nullable: true),
                    BlacklistJson = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVoicePreference", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomVoiceChannel");

            migrationBuilder.DropTable(
                name: "CustomVoiceConfig");

            migrationBuilder.DropTable(
                name: "UserVoicePreference");
        }
    }
}
