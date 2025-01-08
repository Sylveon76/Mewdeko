using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MoreMoreTicketStuff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal[]>(
                name: "ViewerRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: true,
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]");

            migrationBuilder.AlterColumn<decimal[]>(
                name: "SupportRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: true,
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]");

            migrationBuilder.AlterColumn<List<string>>(
                name: "RequiredTags",
                table: "TicketButtons",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string>(
                name: "PreCreateMessage",
                table: "TicketButtons",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "TicketButtons",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultPriorityId",
                table: "TicketButtons",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal[]>(
                name: "AutoAddUserIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: true,
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]");

            migrationBuilder.AlterColumn<decimal[]>(
                name: "AutoAddRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: true,
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]");

            migrationBuilder.AlterColumn<List<string>>(
                name: "AllowedPriorityIds",
                table: "TicketButtons",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal[]>(
                name: "ViewerRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0],
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal[]>(
                name: "SupportRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0],
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<List<string>>(
                name: "RequiredTags",
                table: "TicketButtons",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreCreateMessage",
                table: "TicketButtons",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "TicketButtons",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultPriorityId",
                table: "TicketButtons",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal[]>(
                name: "AutoAddUserIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0],
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal[]>(
                name: "AutoAddRoleIds",
                table: "TicketButtons",
                type: "numeric(20,0)[]",
                nullable: false,
                defaultValue: new decimal[0],
                oldClrType: typeof(decimal[]),
                oldType: "numeric(20,0)[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<List<string>>(
                name: "AllowedPriorityIds",
                table: "TicketButtons",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);
        }
    }
}
