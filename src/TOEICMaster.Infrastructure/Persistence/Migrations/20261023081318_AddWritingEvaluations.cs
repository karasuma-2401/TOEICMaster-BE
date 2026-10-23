using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "writing_evaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WritingAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RubricVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ConsentAcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    QueuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    EstimatedScore = table.Column<int>(type: "integer", nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StrengthsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ImprovementsJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_writing_evaluations_writing_attempts_WritingAttemptId",
                        column: x => x.WritingAttemptId,
                        principalTable: "writing_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "writing_evaluation_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WritingEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionPosition = table.Column<int>(type: "integer", nullable: false),
                    Feedback = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    SuggestedRevision = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_evaluation_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_writing_evaluation_feedback_writing_evaluations_WritingEval~",
                        column: x => x.WritingEvaluationId,
                        principalTable: "writing_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_writing_evaluation_feedback_WritingEvaluationId_QuestionPos~",
                table: "writing_evaluation_feedback",
                columns: new[] { "WritingEvaluationId", "QuestionPosition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_writing_evaluations_LeaseExpiresAtUtc",
                table: "writing_evaluations",
                column: "LeaseExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_writing_evaluations_Status_NextAttemptAtUtc",
                table: "writing_evaluations",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_writing_evaluations_WritingAttemptId",
                table: "writing_evaluations",
                column: "WritingAttemptId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "writing_evaluation_feedback");

            migrationBuilder.DropTable(
                name: "writing_evaluations");
        }
    }
}
