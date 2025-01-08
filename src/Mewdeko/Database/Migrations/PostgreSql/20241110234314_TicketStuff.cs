using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class TicketStuff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketButtons_TicketPanels_TicketPanelId",
                table: "TicketButtons");

            migrationBuilder.DropIndex(
                name: "IX_TicketButtons_TicketPanelId",
                table: "TicketButtons");

            migrationBuilder.RenameColumn(
                name: "TicketPanelId",
                table: "TicketButtons",
                newName: "MaxActiveTickets");

            migrationBuilder.AlterColumn<string>(
                name: "MessageJson",
                table: "TicketPanels",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "TicketButtons",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<List<string>>(
                name: "AllowedPriorityIds",
                table: "TicketButtons",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ArchiveCategoryId",
                table: "TicketButtons",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "AutoAddRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0]);

            migrationBuilder.AddColumn<decimal[]>(
                name: "AutoAddUserIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0]);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AutoCloseTime",
                table: "TicketButtons",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CategoryId",
                table: "TicketButtons",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChannelNameFormat",
                table: "TicketButtons",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Cooldown",
                table: "TicketButtons",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultPriorityId",
                table: "TicketButtons",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "EmbedColor",
                table: "TicketButtons",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "PreCreateMessage",
                table: "TicketButtons",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RequireConfirmation",
                table: "TicketButtons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "RequiredTags",
                table: "TicketButtons",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<bool>(
                name: "SaveTranscript",
                table: "TicketButtons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal[]>(
                name: "SupportRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0]);

            migrationBuilder.AddColumn<decimal[]>(
                name: "ViewerRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0]);

            migrationBuilder.CreateTable(
                name: "StaffNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EnableDmNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyForPriorities = table.Column<List<string>>(type: "text[]", nullable: false),
                    NotifyForTags = table.Column<List<string>>(type: "text[]", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffNotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CaseName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BlacklistedUsers = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    DefaultAutoCloseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DefaultCooldown = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EnableWebhookLogging = table.Column<bool>(type: "boolean", nullable: false),
                    WebhookId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    WebhookToken = table.Column<string>(type: "text", nullable: false),
                    BlacklistedTicketTypes = table.Column<string>(type: "jsonb", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
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
                    Placeholder = table.Column<string>(type: "text", nullable: false),
                    TicketPanelId = table.Column<int>(type: "integer", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
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
                    PriorityId = table.Column<string>(type: "text", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Emoji = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    PingStaff = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredResponseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TicketConfigId = table.Column<int>(type: "integer", nullable: true),
                    EmbedColor = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
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
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TicketConfigId = table.Column<int>(type: "integer", nullable: true),
                    EmbedColor = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
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
                    Label = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Emoji = table.Column<string>(type: "text", nullable: false),
                    OpenMessage = table.Column<string>(type: "text", nullable: false),
                    PreCreateMessage = table.Column<string>(type: "text", nullable: false),
                    RequireConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    ChannelNameFormat = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ArchiveCategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MaxActiveTickets = table.Column<int>(type: "integer", nullable: false),
                    Cooldown = table.Column<TimeSpan>(type: "interval", nullable: true),
                    AutoCloseTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    AutoAddUserIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    AutoAddRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    ViewerRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    SupportRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    AllowedPriorityIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    DefaultPriorityId = table.Column<string>(type: "text", nullable: false),
                    SaveTranscript = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredTags = table.Column<List<string>>(type: "text[]", nullable: false),
                    TicketSelectId = table.Column<int>(type: "integer", nullable: true),
                    EmbedColor = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ButtonId = table.Column<int>(type: "integer", nullable: true),
                    SelectOptionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    TranscriptMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    PriorityId = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    ClaimedBy = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LastActivity = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CaseId = table.Column<int>(type: "integer", nullable: true),
                    TicketCaseId = table.Column<int>(type: "integer", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_TicketButtons_ButtonId",
                        column: x => x.ButtonId,
                        principalTable: "TicketButtons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tickets_TicketCases_TicketCaseId",
                        column: x => x.TicketCaseId,
                        principalTable: "TicketCases",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tickets_TicketSelectOptions_SelectOptionId",
                        column: x => x.SelectOptionId,
                        principalTable: "TicketSelectOptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketNote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketNote_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketNote_TicketId",
                table: "TicketNote",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPriorities_TicketConfigId",
                table: "TicketPriorities",
                column: "TicketConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ButtonId",
                table: "Tickets",
                column: "ButtonId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SelectOptionId",
                table: "Tickets",
                column: "SelectOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketCaseId",
                table: "Tickets",
                column: "TicketCaseId");

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
                name: "FK_TicketButtons_TicketPanels_Id",
                table: "TicketButtons",
                column: "Id",
                principalTable: "TicketPanels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketButtons_TicketPanels_Id",
                table: "TicketButtons");

            migrationBuilder.DropTable(
                name: "StaffNotificationPreferences");

            migrationBuilder.DropTable(
                name: "TicketNote");

            migrationBuilder.DropTable(
                name: "TicketPriorities");

            migrationBuilder.DropTable(
                name: "TicketTag");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketConfigs");

            migrationBuilder.DropTable(
                name: "TicketCases");

            migrationBuilder.DropTable(
                name: "TicketSelectOptions");

            migrationBuilder.DropTable(
                name: "TicketSelects");

            migrationBuilder.DropColumn(
                name: "AllowedPriorityIds",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "ArchiveCategoryId",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "AutoAddRoleIds",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "AutoAddUserIds",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "AutoCloseTime",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "ChannelNameFormat",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "Cooldown",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "DefaultPriorityId",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "EmbedColor",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "PreCreateMessage",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "RequireConfirmation",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "RequiredTags",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "SaveTranscript",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "SupportRoleIds",
                table: "TicketButtons");

            migrationBuilder.DropColumn(
                name: "ViewerRoleIds",
                table: "TicketButtons");

            migrationBuilder.RenameColumn(
                name: "MaxActiveTickets",
                table: "TicketButtons",
                newName: "TicketPanelId");

            migrationBuilder.AlterColumn<string>(
                name: "MessageJson",
                table: "TicketPanels",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "TicketButtons",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "IX_TicketButtons_TicketPanelId",
                table: "TicketButtons",
                column: "TicketPanelId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketButtons_TicketPanels_TicketPanelId",
                table: "TicketButtons",
                column: "TicketPanelId",
                principalTable: "TicketPanels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
