using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWritingAdministration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "writing_media_assets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ObjectKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ByteLength = table.Column<long>(type: "bigint", nullable: false),
                UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_writing_media_assets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "writing_tests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Status = table.Column<short>(type: "smallint", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_writing_tests", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "writing_exercises",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Part = table.Column<short>(type: "smallint", nullable: false),
                Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Topic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Difficulty = table.Column<short>(type: "smallint", nullable: false),
                Status = table.Column<short>(type: "smallint", nullable: false),
                RubricNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                ModelAnswer = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                ImageAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                ImageAlt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                KeywordOne = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                KeywordTwo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Sender = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                EmailBody = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: true),
                Directions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                MinimumWordCount = table.Column<int>(type: "integer", nullable: true),
                Outline = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_writing_exercises", x => x.Id);
                table.ForeignKey(
                    name: "FK_writing_exercises_writing_media_assets_ImageAssetId",
                    column: x => x.ImageAssetId,
                    principalTable: "writing_media_assets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "writing_test_questions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WritingTestId = table.Column<Guid>(type: "uuid", nullable: false),
                ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                Part = table.Column<short>(type: "smallint", nullable: false),
                SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                SnapshotJson = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_writing_test_questions", x => x.Id);
                table.ForeignKey(
                    name: "FK_writing_test_questions_writing_exercises_ExerciseId",
                    column: x => x.ExerciseId,
                    principalTable: "writing_exercises",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_writing_test_questions_writing_tests_WritingTestId",
                    column: x => x.WritingTestId,
                    principalTable: "writing_tests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_writing_exercises_Code",
            table: "writing_exercises",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_writing_exercises_ImageAssetId",
            table: "writing_exercises",
            column: "ImageAssetId");

        migrationBuilder.CreateIndex(
            name: "IX_writing_exercises_Part_Status_UpdatedAtUtc",
            table: "writing_exercises",
            columns: new[] { "Part", "Status", "UpdatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_writing_media_assets_ObjectKey",
            table: "writing_media_assets",
            column: "ObjectKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_writing_test_questions_ExerciseId",
            table: "writing_test_questions",
            column: "ExerciseId");

        migrationBuilder.CreateIndex(
            name: "IX_writing_test_questions_WritingTestId_Part_SequenceNumber",
            table: "writing_test_questions",
            columns: new[] { "WritingTestId", "Part", "SequenceNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_writing_tests_Code",
            table: "writing_tests",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_writing_tests_Status_UpdatedAtUtc",
            table: "writing_tests",
            columns: new[] { "Status", "UpdatedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "writing_test_questions");

        migrationBuilder.DropTable(
            name: "writing_exercises");

        migrationBuilder.DropTable(
            name: "writing_tests");

        migrationBuilder.DropTable(
            name: "writing_media_assets");
    }
}
