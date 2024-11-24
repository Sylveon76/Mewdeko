using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class changeIdType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First drop the identity property
            migrationBuilder.Sql("ALTER TABLE \"MessageCounts\" ALTER COLUMN \"Id\" DROP IDENTITY IF EXISTS;");

            // Change column type
            migrationBuilder.AlterColumn<long>(
                    name: "Id",
                    table: "MessageCounts",
                    type: "bigint",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Check for values that won't fit in int
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ""MessageCounts"" WHERE ""Id"" > 2147483647) THEN
                        RAISE EXCEPTION 'Cannot downgrade: Some IDs are too large for integer type';
                    END IF;
                END
                $$;");

            // Drop identity first
            migrationBuilder.Sql("ALTER TABLE \"MessageCounts\" ALTER COLUMN \"Id\" DROP IDENTITY IF EXISTS;");

            // Change back to int with identity
            migrationBuilder.AlterColumn<int>(
                    name: "Id",
                    table: "MessageCounts",
                    type: "integer",
                    nullable: false,
                    oldClrType: typeof(long),
                    oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}