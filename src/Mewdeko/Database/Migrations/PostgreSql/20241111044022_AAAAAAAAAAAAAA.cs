using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AAAAAAAAAAAAAA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketButtons_ButtonId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketCases_TicketCaseId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketSelectOptions_SelectOptionId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "StaffNotificationPreferences");

            migrationBuilder.DropTable(
                name: "TicketButtons");

            migrationBuilder.DropTable(
                name: "TicketPriorities");

            migrationBuilder.DropTable(
                name: "TicketSelectOptions");

            migrationBuilder.DropTable(
                name: "TicketTag");

            migrationBuilder.DropTable(
                name: "TicketSelects");

            migrationBuilder.DropTable(
                name: "TicketConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_TicketCaseId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PriorityId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TicketCaseId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TranscriptMessageId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "TicketPanels");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "TicketCases");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Tickets",
                newName: "CreatorId");

            migrationBuilder.RenameColumn(
                name: "LastActivity",
                table: "Tickets",
                newName: "LastActivityAt");

            migrationBuilder.RenameColumn(
                name: "MessageJson",
                table: "TicketPanels",
                newName: "EmbedJson");

            migrationBuilder.RenameColumn(
                name: "DateAdded",
                table: "TicketCases",
                newName: "ClosedAt");

            migrationBuilder.RenameColumn(
                name: "CaseName",
                table: "TicketCases",
                newName: "Title");

            migrationBuilder.AlterColumn<List<string>>(
                name: "Tags",
                table: "Tickets",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AddColumn<string>(
                name: "ModalResponses",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptUrl",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MessageId",
                table: "TicketPanels",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TicketCases",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "CaseNote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CaseId = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseNote_TicketCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "TicketCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildTicketSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DefaultAutoCloseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DefaultResponseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DefaultMaxTickets = table.Column<int>(type: "integer", nullable: false),
                    LogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    TranscriptChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BlacklistedUsers = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    EnableStaffPings = table.Column<bool>(type: "boolean", nullable: false),
                    EnableDmNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationRoles = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildTicketSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PanelButtons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PanelId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Emoji = table.Column<string>(type: "text", nullable: true),
                    CustomId = table.Column<string>(type: "text", nullable: false),
                    Style = table.Column<int>(type: "integer", nullable: false),
                    OpenMessageJson = table.Column<string>(type: "text", nullable: true),
                    ModalJson = table.Column<string>(type: "text", nullable: true),
                    ChannelNameFormat = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ArchiveCategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SupportRoles = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    ViewerRoles = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    AutoCloseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RequiredResponseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    MaxActiveTickets = table.Column<int>(type: "integer", nullable: false),
                    AllowedPriorities = table.Column<List<string>>(type: "text[]", nullable: true),
                    DefaultPriority = table.Column<string>(type: "text", nullable: true),
                    SaveTranscript = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PanelButtons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PanelButtons_TicketPanels_Id",
                        column: x => x.Id,
                        principalTable: "TicketPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PanelButtons_TicketPanels_PanelId",
                        column: x => x.PanelId,
                        principalTable: "TicketPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PanelSelectMenu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PanelId = table.Column<int>(type: "integer", nullable: false),
                    CustomId = table.Column<string>(type: "text", nullable: false),
                    Placeholder = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PanelSelectMenu", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PanelSelectMenu_TicketPanels_PanelId",
                        column: x => x.PanelId,
                        principalTable: "TicketPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteEdit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OldContent = table.Column<string>(type: "text", nullable: false),
                    NewContent = table.Column<string>(type: "text", nullable: false),
                    EditorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CaseNoteId = table.Column<int>(type: "integer", nullable: true),
                    TicketNoteId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteEdit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteEdit_CaseNote_CaseNoteId",
                        column: x => x.CaseNoteId,
                        principalTable: "CaseNote",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NoteEdit_TicketNote_TicketNoteId",
                        column: x => x.TicketNoteId,
                        principalTable: "TicketNote",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SelectMenuOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SelectMenuId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Emoji = table.Column<string>(type: "text", nullable: true),
                    OpenMessageJson = table.Column<string>(type: "text", nullable: true),
                    ModalJson = table.Column<string>(type: "text", nullable: true),
                    ChannelNameFormat = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ArchiveCategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SupportRoles = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    ViewerRoles = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    AutoCloseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RequiredResponseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    MaxActiveTickets = table.Column<int>(type: "integer", nullable: false),
                    AllowedPriorities = table.Column<List<string>>(type: "text[]", nullable: true),
                    DefaultPriority = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelectMenuOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SelectMenuOptions_PanelSelectMenu_SelectMenuId",
                        column: x => x.SelectMenuId,
                        principalTable: "PanelSelectMenu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CaseId",
                table: "Tickets",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseNote_CaseId",
                table: "CaseNote",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteEdit_CaseNoteId",
                table: "NoteEdit",
                column: "CaseNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteEdit_TicketNoteId",
                table: "NoteEdit",
                column: "TicketNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PanelButtons_PanelId",
                table: "PanelButtons",
                column: "PanelId");

            migrationBuilder.CreateIndex(
                name: "IX_PanelSelectMenu_PanelId",
                table: "PanelSelectMenu",
                column: "PanelId");

            migrationBuilder.CreateIndex(
                name: "IX_SelectMenuOptions_SelectMenuId",
                table: "SelectMenuOptions",
                column: "SelectMenuId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_PanelButtons_ButtonId",
                table: "Tickets",
                column: "ButtonId",
                principalTable: "PanelButtons",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_SelectMenuOptions_SelectOptionId",
                table: "Tickets",
                column: "SelectOptionId",
                principalTable: "SelectMenuOptions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketCases_CaseId",
                table: "Tickets",
                column: "CaseId",
                principalTable: "TicketCases",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_PanelButtons_ButtonId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_SelectMenuOptions_SelectOptionId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketCases_CaseId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "GuildTicketSettings");

            migrationBuilder.DropTable(
                name: "NoteEdit");

            migrationBuilder.DropTable(
                name: "PanelButtons");

            migrationBuilder.DropTable(
                name: "SelectMenuOptions");

            migrationBuilder.DropTable(
                name: "CaseNote");

            migrationBuilder.DropTable(
                name: "PanelSelectMenu");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CaseId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ModalResponses",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TranscriptUrl",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "TicketPanels");

            migrationBuilder.RenameColumn(
                name: "LastActivityAt",
                table: "Tickets",
                newName: "LastActivity");

            migrationBuilder.RenameColumn(
                name: "CreatorId",
                table: "Tickets",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "EmbedJson",
                table: "TicketPanels",
                newName: "MessageJson");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "TicketCases",
                newName: "CaseName");

            migrationBuilder.RenameColumn(
                name: "ClosedAt",
                table: "TicketCases",
                newName: "DateAdded");

            migrationBuilder.AlterColumn<List<string>>(
                name: "Tags",
                table: "Tickets",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "Tickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriorityId",
                table: "Tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TicketCaseId",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TranscriptMessageId",
                table: "Tickets",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "TicketPanels",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TicketCases",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "TicketCases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "StaffNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EnableDmNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    NotifyForPriorities = table.Column<List<string>>(type: "text[]", nullable: false),
                    NotifyForTags = table.Column<List<string>>(type: "text[]", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffNotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketButtons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AllowedPriorityIds = table.Column<List<string>>(type: "text[]", nullable: true),
                    ArchiveCategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AutoAddRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    AutoAddUserIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    AutoCloseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelNameFormat = table.Column<string>(type: "text", nullable: false),
                    Cooldown = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DefaultPriorityId = table.Column<string>(type: "text", nullable: true),
                    Emoji = table.Column<string>(type: "text", nullable: true),
                    Label = table.Column<string>(type: "text", nullable: false),
                    MaxActiveTickets = table.Column<int>(type: "integer", nullable: false),
                    OpenMessage = table.Column<string>(type: "text", nullable: false),
                    PreCreateMessage = table.Column<string>(type: "text", nullable: true),
                    RequireConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredTags = table.Column<List<string>>(type: "text[]", nullable: true),
                    SaveTranscript = table.Column<bool>(type: "boolean", nullable: false),
                    SupportRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    ViewerRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    EmbedColor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketButtons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketButtons_TicketPanels_Id",
                        column: x => x.Id,
                        principalTable: "TicketPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BlacklistedUsers = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DefaultAutoCloseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DefaultCooldown = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EnableWebhookLogging = table.Column<bool>(type: "boolean", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    WebhookId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    WebhookToken = table.Column<string>(type: "text", nullable: false),
                    BlacklistedTicketTypes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketSelects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Placeholder = table.Column<string>(type: "text", nullable: false),
                    TicketPanelId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSelects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketSelects_TicketPanels_TicketPanelId",
                        column: x => x.TicketPanelId,
                        principalTable: "TicketPanels",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketPriorities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Emoji = table.Column<string>(type: "text", nullable: true),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PingStaff = table.Column<bool>(type: "boolean", nullable: false),
                    PriorityId = table.Column<string>(type: "text", nullable: true),
                    RequiredResponseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TicketConfigId = table.Column<int>(type: "integer", nullable: true),
                    EmbedColor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPriorities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketPriorities_TicketConfigs_TicketConfigId",
                        column: x => x.TicketConfigId,
                        principalTable: "TicketConfigs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketTag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TicketConfigId = table.Column<int>(type: "integer", nullable: true),
                    EmbedColor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTag_TicketConfigs_TicketConfigId",
                        column: x => x.TicketConfigId,
                        principalTable: "TicketConfigs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketSelectOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllowedPriorityIds = table.Column<List<string>>(type: "text[]", nullable: true),
                    ArchiveCategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AutoAddRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    AutoAddUserIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    AutoCloseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelNameFormat = table.Column<string>(type: "text", nullable: false),
                    Cooldown = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DefaultPriorityId = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Emoji = table.Column<string>(type: "text", nullable: true),
                    Label = table.Column<string>(type: "text", nullable: false),
                    MaxActiveTickets = table.Column<int>(type: "integer", nullable: false),
                    OpenMessage = table.Column<string>(type: "text", nullable: false),
                    PreCreateMessage = table.Column<string>(type: "text", nullable: false),
                    RequireConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredTags = table.Column<List<string>>(type: "text[]", nullable: true),
                    SaveTranscript = table.Column<bool>(type: "boolean", nullable: false),
                    SupportRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    TicketSelectId = table.Column<int>(type: "integer", nullable: true),
                    ViewerRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    EmbedColor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSelectOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketSelectOptions_TicketSelects_TicketSelectId",
                        column: x => x.TicketSelectId,
                        principalTable: "TicketSelects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketCaseId",
                table: "Tickets",
                column: "TicketCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPriorities_TicketConfigId",
                table: "TicketPriorities",
                column: "TicketConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSelectOptions_TicketSelectId",
                table: "TicketSelectOptions",
                column: "TicketSelectId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSelects_TicketPanelId",
                table: "TicketSelects",
                column: "TicketPanelId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTag_TicketConfigId",
                table: "TicketTag",
                column: "TicketConfigId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketButtons_ButtonId",
                table: "Tickets",
                column: "ButtonId",
                principalTable: "TicketButtons",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketCases_TicketCaseId",
                table: "Tickets",
                column: "TicketCaseId",
                principalTable: "TicketCases",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketSelectOptions_SelectOptionId",
                table: "Tickets",
                column: "SelectOptionId",
                principalTable: "TicketSelectOptions",
                principalColumn: "Id");
        }
    }
}
