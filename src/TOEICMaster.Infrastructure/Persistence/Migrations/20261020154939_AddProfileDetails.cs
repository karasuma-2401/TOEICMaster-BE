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

        migrationBuilder.AddColumn<string>(
            name: "Phone",
            table: "users",
            type: "character varying(13)",
            maxLength: 13,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "TargetScore",
            table: "users",
            type: "integer",
            nullable: true);
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
