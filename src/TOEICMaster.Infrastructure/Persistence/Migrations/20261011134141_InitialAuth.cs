using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialAuth : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "user_roles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_user_roles_roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_roles_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_roles_Name",
            table: "roles",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_roles_RoleId",
            table: "user_roles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_users_Email",
            table: "users",
            column: "Email",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_roles");

        migrationBuilder.DropTable(
            name: "roles");

        migrationBuilder.DropTable(
            name: "users");
    }
}
