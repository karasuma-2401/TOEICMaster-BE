using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SeedInitialRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "roles",
            columns: new[] { "Id", "Name", "Description" },
            values: new object[,]
            {
                { new Guid("a7f4a8d2-6e31-4c90-8b12-123456789001"), "Admin", "System administrator" },
                { new Guid("b8e5b9c3-7f42-4da1-9c23-123456789002"), "User", "Regular user" }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("a7f4a8d2-6e31-4c90-8b12-123456789001"));

        migrationBuilder.DeleteData(
            table: "roles",
            keyColumn: "Id",
            keyValue: new Guid("b8e5b9c3-7f42-4da1-9c23-123456789002"));
    }
}
