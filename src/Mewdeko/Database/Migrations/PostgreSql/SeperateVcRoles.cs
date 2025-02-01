using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class SeparateVcRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "VcRoles",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                VoiceChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VcRoles", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            "IX_VcRoles_GuildId",
            "VcRoles",
            "GuildId");

        migrationBuilder.Sql(@"
           INSERT INTO ""VcRoles"" (""GuildId"", ""VoiceChannelId"", ""RoleId"", ""DateAdded"")
           SELECT gc.""GuildId"", vr.""VoiceChannelId"", vr.""RoleId"", vr.""DateAdded""
           FROM ""VcRoleInfo"" vr
           JOIN ""GuildConfigs"" gc ON vr.""GuildConfigId"" = gc.""Id""
       ");

        migrationBuilder.DropTable("VcRoleInfo");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("VcRoles");
    }
}