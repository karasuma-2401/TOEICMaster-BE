using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "writing_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WritingTestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TestTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<short>(type: "smallint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnonymousSessionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CurrentQuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    CurrentPhase = table.Column<short>(type: "smallint", nullable: false),
                    PhaseStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PhaseEndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_attempts", x => x.Id);
                    table.CheckConstraint("CK_writing_attempt_owner", "(\"UserId\" IS NOT NULL AND \"AnonymousSessionHash\" IS NULL) OR (\"UserId\" IS NULL AND \"AnonymousSessionHash\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "writing_attempt_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WritingAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    WritingTestQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Part = table.Column<short>(type: "smallint", nullable: false),
                    PreparationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ResponseSeconds = table.Column<int>(type: "integer", nullable: false),
                    PromptSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Answer = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    LastSavedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_attempt_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_writing_attempt_questions_writing_attempts_WritingAttemptId",
                        column: x => x.WritingAttemptId,
                        principalTable: "writing_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_writing_attempt_questions_WritingAttemptId_Position",
                table: "writing_attempt_questions",
                columns: new[] { "WritingAttemptId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_writing_attempt_questions_WritingTestQuestionId",
                table: "writing_attempt_questions",
                column: "WritingTestQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_writing_attempts_AnonymousSessionHash_UpdatedAtUtc",
                table: "writing_attempts",
                columns: new[] { "AnonymousSessionHash", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_writing_attempts_UserId_UpdatedAtUtc",
                table: "writing_attempts",
                columns: new[] { "UserId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_writing_attempts_WritingTestId_CreatedAtUtc",
                table: "writing_attempts",
                columns: new[] { "WritingTestId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "writing_attempt_questions");

            migrationBuilder.DropTable(
                name: "writing_attempts");
        }
    }
}
