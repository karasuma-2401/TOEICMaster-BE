using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLrPracticeAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lr_practice_attempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                CorrectCount = table.Column<int>(type: "integer", nullable: false),
                IncorrectCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lr_practice_attempts", x => x.Id);
                table.ForeignKey(
                    name: "FK_lr_practice_attempts_lr_exercise_bank_items_ItemId",
                    column: x => x.ItemId,
                    principalTable: "lr_exercise_bank_items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_lr_practice_attempts_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "lr_practice_attempt_answers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false),
                SelectedKey = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                CorrectKey = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                Translation = table.Column<string>(type: "text", nullable: true),
                Explanation = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lr_practice_attempt_answers", x => x.Id);
                table.ForeignKey(
                    name: "FK_lr_practice_attempt_answers_lr_practice_attempts_AttemptId",
                    column: x => x.AttemptId,
                    principalTable: "lr_practice_attempts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lr_practice_attempt_answers_AttemptId_QuestionId",
            table: "lr_practice_attempt_answers",
            columns: new[] { "AttemptId", "QuestionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_lr_practice_attempts_ItemId",
            table: "lr_practice_attempts",
            column: "ItemId");

        migrationBuilder.CreateIndex(
            name: "IX_lr_practice_attempts_SubmittedAtUtc",
            table: "lr_practice_attempts",
            column: "SubmittedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_lr_practice_attempts_UserId_ItemId",
            table: "lr_practice_attempts",
            columns: new[] { "UserId", "ItemId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "lr_practice_attempt_answers");

        migrationBuilder.DropTable(
            name: "lr_practice_attempts");
    }
}
