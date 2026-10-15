using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RefreshTokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReplacedBySessionId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_sessions_user_sessions_ReplacedBySessionId",
                    column: x => x.ReplacedBySessionId,
                    principalTable: "user_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_user_sessions_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_sessions_RefreshTokenHash",
            table: "user_sessions",
            column: "RefreshTokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_sessions_ReplacedBySessionId",
            table: "user_sessions",
            column: "ReplacedBySessionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_sessions_UserId_ExpiresAtUtc",
            table: "user_sessions",
            columns: new[] { "UserId", "ExpiresAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_sessions");
    }
}
