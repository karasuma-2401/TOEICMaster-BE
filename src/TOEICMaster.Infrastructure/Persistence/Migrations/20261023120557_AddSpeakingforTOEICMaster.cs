using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSpeakingforTOEICMaster : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "speaking_media_assets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                object_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                size_bytes = table.Column<long>(type: "bigint", nullable: false),
                sha256 = table.Column<string>(type: "character(64)", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_speaking_media_assets", x => x.id);
                table.CheckConstraint("ck_speaking_media_assets_kind", "kind IN ('IMAGE', 'RECORDING')");
                table.CheckConstraint("ck_speaking_media_assets_size_bytes", "size_bytes > 0");
            });

        migrationBuilder.CreateTable(
            name: "speaking_tests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                version = table.Column<int>(type: "integer", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_speaking_tests", x => x.id);
                table.CheckConstraint("ck_speaking_tests_published_at", "status <> 'PUBLISHED' OR published_at_utc IS NOT NULL");
                table.CheckConstraint("ck_speaking_tests_status", "status IN ('DRAFT', 'PUBLISHED')");
            });

        migrationBuilder.CreateTable(
            name: "speaking_exercises",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                part = table.Column<short>(type: "smallint", nullable: false),
                code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                topic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                difficulty = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                content_json = table.Column<string>(type: "jsonb", nullable: false),
                image_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                rubric_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                model_answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_speaking_exercises", x => x.id);
                table.CheckConstraint("ck_speaking_exercises_difficulty", "difficulty IN ('EASY', 'MEDIUM', 'HARD')");
                table.CheckConstraint("ck_speaking_exercises_image", "(part = 2) = (image_asset_id IS NOT NULL)");
                table.CheckConstraint("ck_speaking_exercises_part", "part BETWEEN 1 AND 5");
                table.CheckConstraint("ck_speaking_exercises_status", "status IN ('DRAFT', 'PUBLISHED', 'ARCHIVED')");
                table.ForeignKey(
                    name: "FK_speaking_exercises_speaking_media_assets_image_asset_id",
                    column: x => x.image_asset_id,
                    principalTable: "speaking_media_assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "speaking_attempts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                test_id = table.Column<Guid>(type: "uuid", nullable: false),
                mode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                anonymous_session_hash = table.Column<string>(type: "character(64)", nullable: true),
                current_position = table.Column<short>(type: "smallint", nullable: true),
                current_phase = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                phase_started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                phase_ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_speaking_attempts", x => x.id);
                table.CheckConstraint("ck_speaking_attempts_current_phase", "current_phase IS NULL OR current_phase IN ('READING', 'PREPARING', 'RESPONDING')");
                table.CheckConstraint("ck_speaking_attempts_mode", "mode IN ('FULL', 'PART_1', 'PART_2', 'PART_3', 'PART_4', 'PART_5')");
                table.CheckConstraint("ck_speaking_attempts_owner", "(owner_user_id IS NOT NULL) <> (anonymous_session_hash IS NOT NULL)");
                table.CheckConstraint("ck_speaking_attempts_status", "status IN ('IN_PROGRESS', 'SUBMITTED')");
                table.ForeignKey(
                    name: "FK_speaking_attempts_speaking_tests_test_id",
                    column: x => x.test_id,
                    principalTable: "speaking_tests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "speaking_test_exercises",
            columns: table => new
            {
                test_id = table.Column<Guid>(type: "uuid", nullable: false),
                part = table.Column<short>(type: "smallint", nullable: false),
                sequence_number = table.Column<short>(type: "smallint", nullable: false),
                exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                snapshot_json = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_speaking_test_exercises", x => new { x.test_id, x.part, x.sequence_number });
                table.CheckConstraint("ck_speaking_test_exercises_part", "part BETWEEN 1 AND 5");
                table.CheckConstraint("ck_speaking_test_exercises_sequence_number", "sequence_number >= 1");
                table.ForeignKey(
                    name: "FK_speaking_test_exercises_speaking_exercises_exercise_id",
                    column: x => x.exercise_id,
                    principalTable: "speaking_exercises",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_speaking_test_exercises_speaking_tests_test_id",
                    column: x => x.test_id,
                    principalTable: "speaking_tests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "speaking_attempt_questions",
            columns: table => new
            {
                attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<short>(type: "smallint", nullable: false),
                part = table.Column<short>(type: "smallint", nullable: false),
                question_index = table.Column<short>(type: "smallint", nullable: false),
                exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                content_json = table.Column<string>(type: "jsonb", nullable: false),
                review_json = table.Column<string>(type: "jsonb", nullable: false),
                reading_seconds = table.Column<short>(type: "smallint", nullable: false),
                preparation_seconds = table.Column<short>(type: "smallint", nullable: false),
                response_seconds = table.Column<short>(type: "smallint", nullable: false),
                locked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                recording_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                recording_duration_seconds = table.Column<short>(type: "smallint", nullable: true),
                recording_uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_speaking_attempt_questions", x => new { x.attempt_id, x.position });
                table.CheckConstraint("ck_speaking_attempt_questions_part", "part BETWEEN 1 AND 5");
                table.CheckConstraint("ck_speaking_attempt_questions_question_index", "question_index BETWEEN 0 AND 2");
                table.ForeignKey(
                    name: "FK_speaking_attempt_questions_speaking_attempts_attempt_id",
                    column: x => x.attempt_id,
                    principalTable: "speaking_attempts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_speaking_attempt_questions_speaking_media_assets_recording_~",
                    column: x => x.recording_asset_id,
                    principalTable: "speaking_media_assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_speaking_attempt_questions_recording_asset_id",
            table: "speaking_attempt_questions",
            column: "recording_asset_id");

        migrationBuilder.CreateIndex(
            name: "ix_speaking_attempts_anonymous_session_hash_created_at_utc",
            table: "speaking_attempts",
            columns: new[] { "anonymous_session_hash", "created_at_utc" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_speaking_attempts_owner_user_id_created_at_utc",
            table: "speaking_attempts",
            columns: new[] { "owner_user_id", "created_at_utc" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_speaking_attempts_test_id",
            table: "speaking_attempts",
            column: "test_id");

        migrationBuilder.CreateIndex(
            name: "IX_speaking_exercises_image_asset_id",
            table: "speaking_exercises",
            column: "image_asset_id");

        migrationBuilder.CreateIndex(
            name: "ix_speaking_exercises_part_status_updated_at_utc_id",
            table: "speaking_exercises",
            columns: new[] { "part", "status", "updated_at_utc", "id" },
            descending: new[] { false, false, true, false });

        migrationBuilder.CreateIndex(
            name: "IX_speaking_media_assets_object_key",
            table: "speaking_media_assets",
            column: "object_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_speaking_test_exercises_exercise_id",
            table: "speaking_test_exercises",
            column: "exercise_id");

        migrationBuilder.CreateIndex(
            name: "IX_speaking_test_exercises_test_id_exercise_id",
            table: "speaking_test_exercises",
            columns: new[] { "test_id", "exercise_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_speaking_tests_status_updated_at_utc_id",
            table: "speaking_tests",
            columns: new[] { "status", "updated_at_utc", "id" },
            descending: new[] { false, true, false });

        // Codes are unique regardless of case; EF Core cannot model expression indexes.
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX ux_speaking_exercises_upper_code ON speaking_exercises (upper(code));");
        migrationBuilder.Sql("CREATE UNIQUE INDEX ux_speaking_tests_upper_code ON speaking_tests (upper(code));");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "speaking_attempt_questions");

        migrationBuilder.DropTable(
            name: "speaking_test_exercises");

        migrationBuilder.DropTable(
            name: "speaking_attempts");

        migrationBuilder.DropTable(
            name: "speaking_exercises");

        migrationBuilder.DropTable(
            name: "speaking_tests");

        migrationBuilder.DropTable(
            name: "speaking_media_assets");
    }
}
