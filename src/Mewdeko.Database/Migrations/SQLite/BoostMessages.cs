﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class Boostmessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "BoostMessage",
            "GuildConfigs",
            "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<ulong>(
            "BoostMessageChannelId",
            "GuildConfigs",
            "INTEGER",
            nullable: false,
            defaultValue: 0ul);

        migrationBuilder.AddColumn<int>(
            "BoostMessageDeleteAfter",
            "GuildConfigs",
            "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            "SendBoostMessage",
            "GuildConfigs",
            "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "BoostMessage",
            "GuildConfigs");

        migrationBuilder.DropColumn(
            "BoostMessageChannelId",
            "GuildConfigs");

        migrationBuilder.DropColumn(
            "BoostMessageDeleteAfter",
            "GuildConfigs");

        migrationBuilder.DropColumn(
            "SendBoostMessage",
            "GuildConfigs");
    }
}