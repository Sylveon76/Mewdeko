using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MigrateToMultiStarboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Starboard");

            migrationBuilder.CreateTable(
                name: "StarboardPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PostId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StarboardConfigId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarboardPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Starboards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StarboardChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Emote = table.Column<string>(type: "text", nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    CheckedChannels = table.Column<string>(type: "text", nullable: false),
                    UseBlacklist = table.Column<bool>(type: "boolean", nullable: false),
                    AllowBots = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveOnDelete = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveOnReactionsClear = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveOnBelowThreshold = table.Column<bool>(type: "boolean", nullable: false),
                    RepostThreshold = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Starboards", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StarboardPosts");

            migrationBuilder.DropTable(
                name: "Starboards");

            migrationBuilder.CreateTable(
                name: "Starboard",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PostId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Starboard", x => x.Id);
                });
        }
    }
}
