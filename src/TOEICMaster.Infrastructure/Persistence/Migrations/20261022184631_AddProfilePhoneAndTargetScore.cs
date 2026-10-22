using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProfilePhoneAndTargetScore : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
            name: "Phone",
            table: "users");

        migrationBuilder.DropColumn(
            name: "TargetScore",
            table: "users");
    }
}
