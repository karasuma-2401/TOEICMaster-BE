using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPasswordReset : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "password_reset_challenges",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CodeHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                ResetTokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                ResetTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_password_reset_challenges", x => x.UserId);
                table.ForeignKey(
                    name: "FK_password_reset_challenges_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_password_reset_challenges_ResetTokenHash",
            table: "password_reset_challenges",
            column: "ResetTokenHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "password_reset_challenges");
    }
}
