using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSpeakingPracticeAndAiEvaluation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "test_id",
            table: "speaking_attempts",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<Guid>(
            name: "exercise_id",
            table: "speaking_attempts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "speaking_evaluations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                speech_provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                speech_locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                language_provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                language_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                rubric_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                consent_accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                queued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retry_count = table.Column<int>(type: "integer", nullable: false),
                estimated_score = table.Column<int>(type: "integer", nullable: true),
                summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                strengths_json = table.Column<string>(type: "jsonb", nullable: true),
                improvements_json = table.Column<string>(type: "jsonb", nullable: true),
                failure_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                failure_message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_speaking_evaluations", x => x.id);
                table.CheckConstraint("ck_speaking_evaluations_estimated_score", "estimated_score IS NULL OR (estimated_score BETWEEN 0 AND 200 AND estimated_score % 10 = 0)");
                table.CheckConstraint("ck_speaking_evaluations_retry_count", "retry_count >= 0");
                table.CheckConstraint("ck_speaking_evaluations_status", "status IN ('QUEUED', 'PROCESSING', 'RETRYING', 'COMPLETED', 'FAILED')");
                table.ForeignKey(
                    name: "FK_speaking_evaluations_speaking_attempts_attempt_id",
                    column: x => x.attempt_id,
                    principalTable: "speaking_attempts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "speaking_evaluation_items",
            columns: table => new
            {
                evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<short>(type: "smallint", nullable: false),
                part = table.Column<short>(type: "smallint", nullable: false),
                recognition_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                transcript = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                audio_seconds = table.Column<double>(type: "double precision", nullable: false),
                accuracy_score = table.Column<double>(type: "double precision", nullable: false),
                fluency_score = table.Column<double>(type: "double precision", nullable: false),
                completeness_score = table.Column<double>(type: "double precision", nullable: true),
                prosody_score = table.Column<double>(type: "double precision", nullable: true),
                pronunciation_score = table.Column<double>(type: "double precision", nullable: false),
                word_issues_json = table.Column<string>(type: "jsonb", nullable: false),
                grammar_score = table.Column<short>(type: "smallint", nullable: true),
                vocabulary_score = table.Column<short>(type: "smallint", nullable: true),
                relevance_score = table.Column<short>(type: "smallint", nullable: true),
                raw_score = table.Column<short>(type: "smallint", nullable: true),
                max_raw_score = table.Column<short>(type: "smallint", nullable: false),
                feedback = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                suggested_response = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_speaking_evaluation_items", x => new { x.evaluation_id, x.position });
                table.CheckConstraint("ck_speaking_evaluation_items_language_scores", "(grammar_score IS NULL OR grammar_score BETWEEN 0 AND 100) AND (vocabulary_score IS NULL OR vocabulary_score BETWEEN 0 AND 100) AND (relevance_score IS NULL OR relevance_score BETWEEN 0 AND 100)");
                table.CheckConstraint("ck_speaking_evaluation_items_max_raw_score", "max_raw_score IN (3, 5)");
                table.CheckConstraint("ck_speaking_evaluation_items_part", "part BETWEEN 1 AND 5");
                table.CheckConstraint("ck_speaking_evaluation_items_raw_score", "raw_score IS NULL OR raw_score BETWEEN 0 AND max_raw_score");
                table.CheckConstraint("ck_speaking_evaluation_items_recognition_status", "recognition_status IN ('RECOGNIZED', 'NO_SPEECH', 'NO_RECORDING')");
                table.ForeignKey(
                    name: "FK_speaking_evaluation_items_speaking_evaluations_evaluation_id",
                    column: x => x.evaluation_id,
                    principalTable: "speaking_evaluations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_speaking_attempts_exercise_id",
            table: "speaking_attempts",
            column: "exercise_id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_speaking_attempts_source",
            table: "speaking_attempts",
            sql: "(test_id IS NOT NULL) <> (exercise_id IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_speaking_evaluations_attempt_id",
            table: "speaking_evaluations",
            column: "attempt_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_speaking_evaluations_lease_expires_at_utc",
            table: "speaking_evaluations",
            column: "lease_expires_at_utc");

        migrationBuilder.CreateIndex(
            name: "IX_speaking_evaluations_status_next_attempt_at_utc",
            table: "speaking_evaluations",
            columns: new[] { "status", "next_attempt_at_utc" });

        migrationBuilder.AddForeignKey(
            name: "FK_speaking_attempts_speaking_exercises_exercise_id",
            table: "speaking_attempts",
            column: "exercise_id",
            principalTable: "speaking_exercises",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Part-practice attempts have no test and cannot survive test_id becoming required again.
        migrationBuilder.Sql("DELETE FROM speaking_attempts WHERE test_id IS NULL;");

        migrationBuilder.DropForeignKey(
            name: "FK_speaking_attempts_speaking_exercises_exercise_id",
            table: "speaking_attempts");

        migrationBuilder.DropTable(
            name: "speaking_evaluation_items");

        migrationBuilder.DropTable(
            name: "speaking_evaluations");

        migrationBuilder.DropIndex(
            name: "IX_speaking_attempts_exercise_id",
            table: "speaking_attempts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_speaking_attempts_source",
            table: "speaking_attempts");

        migrationBuilder.DropColumn(
            name: "exercise_id",
            table: "speaking_attempts");

        migrationBuilder.AlterColumn<Guid>(
            name: "test_id",
            table: "speaking_attempts",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
