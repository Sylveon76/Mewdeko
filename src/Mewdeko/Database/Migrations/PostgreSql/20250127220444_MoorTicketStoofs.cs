using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MoorTicketStoofs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NoteEdit_CaseNotes_CaseNoteId",
                table: "NoteEdit");

            migrationBuilder.DropForeignKey(
                name: "FK_NoteEdit_TicketNote_TicketNoteId",
                table: "NoteEdit");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketNote_Tickets_TicketId",
                table: "TicketNote");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TicketNote",
                table: "TicketNote");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NoteEdit",
                table: "NoteEdit");

            migrationBuilder.RenameTable(
                name: "TicketNote",
                newName: "TicketNotes");

            migrationBuilder.RenameTable(
                name: "NoteEdit",
                newName: "NoteEdits");

            migrationBuilder.RenameIndex(
                name: "IX_TicketNote_TicketId",
                table: "TicketNotes",
                newName: "IX_TicketNotes_TicketId");

            migrationBuilder.RenameIndex(
                name: "IX_NoteEdit_TicketNoteId",
                table: "NoteEdits",
                newName: "IX_NoteEdits_TicketNoteId");

            migrationBuilder.RenameIndex(
                name: "IX_NoteEdit_CaseNoteId",
                table: "NoteEdits",
                newName: "IX_NoteEdits_CaseNoteId");

            migrationBuilder.AddColumn<bool>(
                name: "SaveTranscript",
                table: "SelectMenuOptions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TicketNotes",
                table: "TicketNotes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NoteEdits",
                table: "NoteEdits",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "TicketPriorities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PriorityId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Emoji = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    PingStaff = table.Column<bool>(type: "boolean", nullable: false),
                    ResponseTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Color = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPriorities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TagId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTags", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_NoteEdits_CaseNotes_CaseNoteId",
                table: "NoteEdits",
                column: "CaseNoteId",
                principalTable: "CaseNotes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NoteEdits_TicketNotes_TicketNoteId",
                table: "NoteEdits",
                column: "TicketNoteId",
                principalTable: "TicketNotes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketNotes_Tickets_TicketId",
                table: "TicketNotes",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NoteEdits_CaseNotes_CaseNoteId",
                table: "NoteEdits");

            migrationBuilder.DropForeignKey(
                name: "FK_NoteEdits_TicketNotes_TicketNoteId",
                table: "NoteEdits");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketNotes_Tickets_TicketId",
                table: "TicketNotes");

            migrationBuilder.DropTable(
                name: "TicketPriorities");

            migrationBuilder.DropTable(
                name: "TicketTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TicketNotes",
                table: "TicketNotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NoteEdits",
                table: "NoteEdits");

            migrationBuilder.DropColumn(
                name: "SaveTranscript",
                table: "SelectMenuOptions");

            migrationBuilder.RenameTable(
                name: "TicketNotes",
                newName: "TicketNote");

            migrationBuilder.RenameTable(
                name: "NoteEdits",
                newName: "NoteEdit");

            migrationBuilder.RenameIndex(
                name: "IX_TicketNotes_TicketId",
                table: "TicketNote",
                newName: "IX_TicketNote_TicketId");

            migrationBuilder.RenameIndex(
                name: "IX_NoteEdits_TicketNoteId",
                table: "NoteEdit",
                newName: "IX_NoteEdit_TicketNoteId");

            migrationBuilder.RenameIndex(
                name: "IX_NoteEdits_CaseNoteId",
                table: "NoteEdit",
                newName: "IX_NoteEdit_CaseNoteId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TicketNote",
                table: "TicketNote",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NoteEdit",
                table: "NoteEdit",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NoteEdit_CaseNotes_CaseNoteId",
                table: "NoteEdit",
                column: "CaseNoteId",
                principalTable: "CaseNotes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NoteEdit_TicketNote_TicketNoteId",
                table: "NoteEdit",
                column: "TicketNoteId",
                principalTable: "TicketNote",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketNote_Tickets_TicketId",
                table: "TicketNote",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
