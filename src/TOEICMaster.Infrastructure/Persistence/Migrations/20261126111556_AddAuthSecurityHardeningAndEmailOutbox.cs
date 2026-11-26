using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAuthSecurityHardeningAndEmailOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SecurityStamp",
            table: "users",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<Guid>(
            name: "FamilyId",
            table: "user_sessions",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<string>(
            name: "PendingDisplayName",
            table: "email_verification_challenges",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PendingPasswordHash",
            table: "email_verification_challenges",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "email_outbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                To = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TextBody = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<short>(type: "smallint", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_email_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_sessions_FamilyId",
            table: "user_sessions",
            column: "FamilyId");

        migrationBuilder.CreateIndex(
            name: "IX_email_outbox_messages_Status_NextAttemptAtUtc",
            table: "email_outbox_messages",
            columns: new[] { "Status", "NextAttemptAtUtc" });

        migrationBuilder.Sql(
            """
            UPDATE users
            SET "SecurityStamp" = md5("Id"::text || random()::text)::uuid
            WHERE "SecurityStamp" = '00000000-0000-0000-0000-000000000000';
            """);

        migrationBuilder.Sql(
            """
            UPDATE user_sessions
            SET "FamilyId" = "Id"
            WHERE "FamilyId" = '00000000-0000-0000-0000-000000000000';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "email_outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_user_sessions_FamilyId",
            table: "user_sessions");

        migrationBuilder.DropColumn(
            name: "SecurityStamp",
            table: "users");

        migrationBuilder.DropColumn(
            name: "FamilyId",
            table: "user_sessions");

        migrationBuilder.DropColumn(
            name: "PendingDisplayName",
            table: "email_verification_challenges");

        migrationBuilder.DropColumn(
            name: "PendingPasswordHash",
            table: "email_verification_challenges");
    }
}
