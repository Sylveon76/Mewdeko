using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MusicStuffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseNote_TicketCases_CaseId",
                table: "CaseNote");

            migrationBuilder.DropForeignKey(
                name: "FK_NoteEdit_CaseNote_CaseNoteId",
                table: "NoteEdit");

            migrationBuilder.DropForeignKey(
                name: "FK_PanelSelectMenu_TicketPanels_PanelId",
                table: "PanelSelectMenu");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistSong_MusicPlaylists_MusicPlaylistId",
                table: "PlaylistSong");

            migrationBuilder.DropForeignKey(
                name: "FK_SelectMenuOptions_PanelSelectMenu_SelectMenuId",
                table: "SelectMenuOptions");

            migrationBuilder.DropIndex(
                name: "IX_PlaylistSong_MusicPlaylistId",
                table: "PlaylistSong");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PanelSelectMenu",
                table: "PanelSelectMenu");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CaseNote",
                table: "CaseNote");

            migrationBuilder.DropColumn(
                name: "Author",
                table: "MusicPlaylists");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "MusicPlaylists");

            migrationBuilder.RenameTable(
                name: "PanelSelectMenu",
                newName: "PanelSelectMenus");

            migrationBuilder.RenameTable(
                name: "CaseNote",
                newName: "CaseNotes");

            migrationBuilder.RenameIndex(
                name: "IX_PanelSelectMenu_PanelId",
                table: "PanelSelectMenus",
                newName: "IX_PanelSelectMenus_PanelId");

            migrationBuilder.RenameIndex(
                name: "IX_CaseNote_CaseId",
                table: "CaseNotes",
                newName: "IX_CaseNotes_CaseId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MusicPlaylists",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "MusicPlaylists",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DjRoleId",
                table: "MusicPlayerSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PanelSelectMenus",
                table: "PanelSelectMenus",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CaseNotes",
                table: "CaseNotes",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "MusicPlaylistTracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlaylistId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Uri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicPlaylistTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicPlaylistTracks_MusicPlaylists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "MusicPlaylists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MusicPlaylists_GuildId_Name",
                table: "MusicPlaylists",
                columns: new[] { "GuildId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MusicPlaylistTracks_PlaylistId_Index",
                table: "MusicPlaylistTracks",
                columns: new[] { "PlaylistId", "Index" });

            migrationBuilder.AddForeignKey(
                name: "FK_CaseNotes_TicketCases_CaseId",
                table: "CaseNotes",
                column: "CaseId",
                principalTable: "TicketCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NoteEdit_CaseNotes_CaseNoteId",
                table: "NoteEdit",
                column: "CaseNoteId",
                principalTable: "CaseNotes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PanelSelectMenus_TicketPanels_PanelId",
                table: "PanelSelectMenus",
                column: "PanelId",
                principalTable: "TicketPanels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SelectMenuOptions_PanelSelectMenus_SelectMenuId",
                table: "SelectMenuOptions",
                column: "SelectMenuId",
                principalTable: "PanelSelectMenus",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseNotes_TicketCases_CaseId",
                table: "CaseNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_NoteEdit_CaseNotes_CaseNoteId",
                table: "NoteEdit");

            migrationBuilder.DropForeignKey(
                name: "FK_PanelSelectMenus_TicketPanels_PanelId",
                table: "PanelSelectMenus");

            migrationBuilder.DropForeignKey(
                name: "FK_SelectMenuOptions_PanelSelectMenus_SelectMenuId",
                table: "SelectMenuOptions");

            migrationBuilder.DropTable(
                name: "MusicPlaylistTracks");

            migrationBuilder.DropIndex(
                name: "IX_MusicPlaylists_GuildId_Name",
                table: "MusicPlaylists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PanelSelectMenus",
                table: "PanelSelectMenus");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CaseNotes",
                table: "CaseNotes");

            migrationBuilder.DropColumn(
                name: "GuildId",
                table: "MusicPlaylists");

            migrationBuilder.DropColumn(
                name: "DjRoleId",
                table: "MusicPlayerSettings");

            migrationBuilder.RenameTable(
                name: "PanelSelectMenus",
                newName: "PanelSelectMenu");

            migrationBuilder.RenameTable(
                name: "CaseNotes",
                newName: "CaseNote");

            migrationBuilder.RenameIndex(
                name: "IX_PanelSelectMenus_PanelId",
                table: "PanelSelectMenu",
                newName: "IX_PanelSelectMenu_PanelId");

            migrationBuilder.RenameIndex(
                name: "IX_CaseNotes_CaseId",
                table: "CaseNote",
                newName: "IX_CaseNote_CaseId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MusicPlaylists",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "MusicPlaylists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "MusicPlaylists",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PanelSelectMenu",
                table: "PanelSelectMenu",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CaseNote",
                table: "CaseNote",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistSong_MusicPlaylistId",
                table: "PlaylistSong",
                column: "MusicPlaylistId");

            migrationBuilder.AddForeignKey(
                name: "FK_CaseNote_TicketCases_CaseId",
                table: "CaseNote",
                column: "CaseId",
                principalTable: "TicketCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NoteEdit_CaseNote_CaseNoteId",
                table: "NoteEdit",
                column: "CaseNoteId",
                principalTable: "CaseNote",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PanelSelectMenu_TicketPanels_PanelId",
                table: "PanelSelectMenu",
                column: "PanelId",
                principalTable: "TicketPanels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistSong_MusicPlaylists_MusicPlaylistId",
                table: "PlaylistSong",
                column: "MusicPlaylistId",
                principalTable: "MusicPlaylists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SelectMenuOptions_PanelSelectMenu_SelectMenuId",
                table: "SelectMenuOptions",
                column: "SelectMenuId",
                principalTable: "PanelSelectMenu",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
