using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSpeakingDirectTestContent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "content_mode",
            table: "speaking_tests",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "EXERCISE_BANK");

        migrationBuilder.AlterColumn<Guid>(
            name: "exercise_id",
            table: "speaking_test_exercises",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<string>(
            name: "content_json",
            table: "speaking_test_exercises",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "image_asset_id",
            table: "speaking_test_exercises",
            type: "uuid",
            nullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "exercise_id",
            table: "speaking_attempt_questions",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddCheckConstraint(
            name: "ck_speaking_tests_content_mode",
            table: "speaking_tests",
            sql: "content_mode IN ('EXERCISE_BANK', 'DIRECT')");

        migrationBuilder.CreateIndex(
            name: "IX_speaking_test_exercises_image_asset_id",
            table: "speaking_test_exercises",
            column: "image_asset_id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_speaking_test_exercises_content_source",
            table: "speaking_test_exercises",
            sql: "(exercise_id IS NOT NULL AND content_json IS NULL) OR (exercise_id IS NULL AND content_json IS NOT NULL)");

        migrationBuilder.AddForeignKey(
            name: "FK_speaking_test_exercises_speaking_media_assets_image_asset_id",
            table: "speaking_test_exercises",
            column: "image_asset_id",
            principalTable: "speaking_media_assets",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM speaking_tests WHERE content_mode = 'DIRECT')
                   OR EXISTS (SELECT 1 FROM speaking_test_exercises WHERE content_json IS NOT NULL)
                   OR EXISTS (SELECT 1 FROM speaking_attempt_questions WHERE exercise_id IS NULL) THEN
                    RAISE EXCEPTION 'Cannot roll back Speaking direct-test migration while direct content or attempts exist.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropForeignKey(
            name: "FK_speaking_test_exercises_speaking_media_assets_image_asset_id",
            table: "speaking_test_exercises");

        migrationBuilder.DropCheckConstraint(
            name: "ck_speaking_tests_content_mode",
            table: "speaking_tests");

        migrationBuilder.DropIndex(
            name: "IX_speaking_test_exercises_image_asset_id",
            table: "speaking_test_exercises");

        migrationBuilder.DropCheckConstraint(
            name: "ck_speaking_test_exercises_content_source",
            table: "speaking_test_exercises");

        migrationBuilder.DropColumn(
            name: "content_mode",
            table: "speaking_tests");

        migrationBuilder.DropColumn(
            name: "content_json",
            table: "speaking_test_exercises");

        migrationBuilder.DropColumn(
            name: "image_asset_id",
            table: "speaking_test_exercises");

        migrationBuilder.AlterColumn<Guid>(
            name: "exercise_id",
            table: "speaking_test_exercises",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "exercise_id",
            table: "speaking_attempt_questions",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
