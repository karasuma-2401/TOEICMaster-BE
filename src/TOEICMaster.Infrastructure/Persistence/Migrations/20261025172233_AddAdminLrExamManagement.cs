using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAdminLrExamManagement : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "exam_media_assets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ObjectKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_media_assets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "exams",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exams", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "score_profiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_score_profiles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "exam_revisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionNumber = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ScoreProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                MasterAudioObjectKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_revisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_exam_revisions_exams_ExamId",
                    column: x => x.ExamId,
                    principalTable: "exams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_exam_revisions_score_profiles_ScoreProfileId",
                    column: x => x.ScoreProfileId,
                    principalTable: "score_profiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "score_conversions",
            columns: table => new
            {
                ScoreProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                Skill = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                RawCorrectCount = table.Column<int>(type: "integer", nullable: false),
                ScaledScore = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_score_conversions", x => new { x.ScoreProfileId, x.Skill, x.RawCorrectCount });
                table.ForeignKey(
                    name: "FK_score_conversions_score_profiles_ScoreProfileId",
                    column: x => x.ScoreProfileId,
                    principalTable: "score_profiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "exam_blocks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Part = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                FirstQuestionNumber = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_blocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_exam_blocks_exam_revisions_RevisionId",
                    column: x => x.RevisionId,
                    principalTable: "exam_revisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "exam_context_blocks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BlockId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Text = table.Column<string>(type: "text", nullable: true),
                ImageAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_context_blocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_exam_context_blocks_exam_blocks_BlockId",
                    column: x => x.BlockId,
                    principalTable: "exam_blocks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_exam_context_blocks_exam_media_assets_ImageAssetId",
                    column: x => x.ImageAssetId,
                    principalTable: "exam_media_assets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "exam_questions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BlockId = table.Column<Guid>(type: "uuid", nullable: false),
                Number = table.Column<int>(type: "integer", nullable: false),
                Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Stem = table.Column<string>(type: "text", nullable: true),
                OptionA = table.Column<string>(type: "text", nullable: true),
                OptionB = table.Column<string>(type: "text", nullable: true),
                OptionC = table.Column<string>(type: "text", nullable: true),
                OptionD = table.Column<string>(type: "text", nullable: true),
                CorrectKey = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                Translation = table.Column<string>(type: "text", nullable: true),
                Explanation = table.Column<string>(type: "text", nullable: true),
                ImageAssetId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_questions", x => x.Id);
                table.ForeignKey(
                    name: "FK_exam_questions_exam_blocks_BlockId",
                    column: x => x.BlockId,
                    principalTable: "exam_blocks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_exam_questions_exam_media_assets_ImageAssetId",
                    column: x => x.ImageAssetId,
                    principalTable: "exam_media_assets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_exam_blocks_RevisionId_FirstQuestionNumber",
            table: "exam_blocks",
            columns: new[] { "RevisionId", "FirstQuestionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_exam_blocks_RevisionId_Key",
            table: "exam_blocks",
            columns: new[] { "RevisionId", "Key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_exam_context_blocks_BlockId_Ordinal",
            table: "exam_context_blocks",
            columns: new[] { "BlockId", "Ordinal" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_exam_context_blocks_ImageAssetId",
            table: "exam_context_blocks",
            column: "ImageAssetId");

        migrationBuilder.CreateIndex(
            name: "IX_exam_media_assets_ObjectKey",
            table: "exam_media_assets",
            column: "ObjectKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_exam_questions_BlockId_Number",
            table: "exam_questions",
            columns: new[] { "BlockId", "Number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_exam_questions_ImageAssetId",
            table: "exam_questions",
            column: "ImageAssetId");

        migrationBuilder.CreateIndex(
            name: "IX_exam_revisions_ExamId_Status",
            table: "exam_revisions",
            columns: new[] { "ExamId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_exam_revisions_ExamId_VersionNumber",
            table: "exam_revisions",
            columns: new[] { "ExamId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_exam_revisions_ScoreProfileId",
            table: "exam_revisions",
            column: "ScoreProfileId");

        migrationBuilder.CreateIndex(
            name: "IX_exams_Code",
            table: "exams",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_score_profiles_Name",
            table: "score_profiles",
            column: "Name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "exam_context_blocks");

        migrationBuilder.DropTable(
            name: "exam_questions");

        migrationBuilder.DropTable(
            name: "score_conversions");

        migrationBuilder.DropTable(
            name: "exam_blocks");

        migrationBuilder.DropTable(
            name: "exam_media_assets");

        migrationBuilder.DropTable(
            name: "exam_revisions");

        migrationBuilder.DropTable(
            name: "exams");

        migrationBuilder.DropTable(
            name: "score_profiles");
    }
}
