using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MoreTicketStuff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal[]>(
                name: "ViewerRoleIds",
                table: "TicketSelectOptions",
                type: "numeric(20,0)[]",
                nullable: true,
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]");

            migrationBuilder.AlterColumn<decimal[]>(
                name: "SupportRoleIds",
                table: "TicketSelectOptions",
                type: "numeric(20,0)[]",
                nullable: true,
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]");

            migrationBuilder.AlterColumn<List<string>>(
                name: "RequiredTags",
                table: "TicketSelectOptions",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "TicketSelectOptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultPriorityId",
                table: "TicketSelectOptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal[]>(
                name: "AutoAddUserIds",
                table: "TicketSelectOptions",
                type: "numeric(20,0)[]",
                nullable: true,
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]");

            migrationBuilder.AlterColumn<decimal[]>(
                name: "AutoAddRoleIds",
                table: "TicketSelectOptions",
                type: "numeric(20,0)[]",
                nullable: true,
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]");

            migrationBuilder.AlterColumn<List<string>>(
                name: "AllowedPriorityIds",
                table: "TicketSelectOptions",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string>(
                name: "PriorityId",
                table: "TicketPriorities",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                table: "TicketPriorities",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "TicketPriorities",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal[]>(
                name: "ViewerRoleIds",
                table: "TicketSelectOptions",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0],
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal[]>(
                name: "SupportRoleIds",
                table: "TicketSelectOptions",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0],
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<List<string>>(
                name: "RequiredTags",
                table: "TicketSelectOptions",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "TicketSelectOptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultPriorityId",
                table: "TicketSelectOptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal[]>(
                name: "AutoAddUserIds",
                table: "TicketSelectOptions",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0],
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal[]>(
                name: "AutoAddRoleIds",
                table: "TicketSelectOptions",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0],
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<List<string>>(
                name: "AllowedPriorityIds",
                table: "TicketSelectOptions",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PriorityId",
                table: "TicketPriorities",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                table: "TicketPriorities",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "TicketPriorities",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
