using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class AddCompleteExamTakingModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExamAttempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                ScoreProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TimeLimitSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 7200),
                IsTimeExpired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ListeningRawScore = table.Column<int>(type: "integer", nullable: true),
                ReadingRawScore = table.Column<int>(type: "integer", nullable: true),
                ListeningScaledScore = table.Column<int>(type: "integer", nullable: true),
                ReadingScaledScore = table.Column<int>(type: "integer", nullable: true),
                TotalScaledScore = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExamAttempts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ExamAttemptAnswers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                QuestionNumber = table.Column<int>(type: "integer", nullable: false),
                Part = table.Column<int>(type: "integer", nullable: false),
                SelectedKey = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                CorrectKey = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                IsFlagged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                AnsweredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExamAttemptAnswers", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExamAttemptAnswers_ExamAttempts_AttemptId",
                    column: x => x.AttemptId,
                    principalTable: "ExamAttempts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExamAttemptAnswers_AttemptId_QuestionId",
            table: "ExamAttemptAnswers",
            columns: new[] { "AttemptId", "QuestionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExamAttempts_StartedAtUtc",
            table: "ExamAttempts",
            column: "StartedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ExamAttempts_UserId_RevisionId",
            table: "ExamAttempts",
            columns: new[] { "UserId", "RevisionId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ExamAttemptAnswers");

        migrationBuilder.DropTable(
            name: "ExamAttempts");
    }
}

