using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProfileDetails : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "DateOfBirth",
            table: "users",
            type: "date",
            nullable: true);

        // This migration and 20261022184631 were created on divergent branches.
        // Preserve both histories by making their shared columns idempotent for PostgreSQL.
        migrationBuilder.Sql("""
            ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "Phone" character varying(13);
            ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "TargetScore" integer;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DateOfBirth",
            table: "users");

        migrationBuilder.DropColumn(
            name: "Phone",
            table: "users");

        migrationBuilder.DropColumn(
            name: "TargetScore",
            table: "users");
    }
}
