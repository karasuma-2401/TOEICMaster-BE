using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWritingIndependentTestsAndPractice : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "ExerciseId",
            table: "writing_test_questions",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<Guid>(
            name: "ImageAssetId",
            table: "writing_test_questions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "WritingTestId",
            table: "writing_attempts",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<bool>(
            name: "IsPractice",
            table: "writing_attempts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "WritingExerciseId",
            table: "writing_attempts",
            type: "uuid",
            nullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "WritingTestQuestionId",
            table: "writing_attempt_questions",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.CreateIndex(
            name: "IX_writing_test_questions_ImageAssetId",
            table: "writing_test_questions",
            column: "ImageAssetId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM writing_test_questions WHERE "ExerciseId" IS NULL)
                   OR EXISTS (SELECT 1 FROM writing_attempts WHERE "IsPractice" = TRUE) THEN
                    RAISE EXCEPTION 'Cannot roll back Writing migration while independent questions or practice attempts exist.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropIndex(
            name: "IX_writing_test_questions_ImageAssetId",
            table: "writing_test_questions");

        migrationBuilder.DropColumn(
            name: "ImageAssetId",
            table: "writing_test_questions");

        migrationBuilder.DropColumn(
            name: "IsPractice",
            table: "writing_attempts");

        migrationBuilder.DropColumn(
            name: "WritingExerciseId",
            table: "writing_attempts");

        migrationBuilder.AlterColumn<Guid>(
            name: "ExerciseId",
            table: "writing_test_questions",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "WritingTestId",
            table: "writing_attempts",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "WritingTestQuestionId",
            table: "writing_attempt_questions",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
