using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEmailVerification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EmailVerifiedAtUtc",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE users
            SET "EmailVerifiedAtUtc" = "CreatedAtUtc"
            WHERE "IsActive" = TRUE;
            """);

        migrationBuilder.CreateTable(
            name: "email_verification_challenges",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CodeHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FailedAttempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_email_verification_challenges", x => x.UserId);
                table.ForeignKey(
                    name: "FK_email_verification_challenges_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "email_verification_challenges");

        migrationBuilder.DropColumn(
            name: "EmailVerifiedAtUtc",
            table: "users");
    }
}
