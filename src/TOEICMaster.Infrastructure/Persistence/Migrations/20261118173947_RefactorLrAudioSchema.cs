using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RefactorLrAudioSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MasterAudioObjectKey",
            table: "exam_revisions");

        migrationBuilder.DropColumn(
            name: "UsesMediaWorkspace",
            table: "exam_revisions");

        migrationBuilder.AddColumn<int>(
            name: "ExpectedDurationMs",
            table: "exam_media_upload_intents",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DurationMs",
            table: "exam_media_assets",
            type: "integer",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExpectedDurationMs",
            table: "exam_media_upload_intents");

        migrationBuilder.DropColumn(
            name: "DurationMs",
            table: "exam_media_assets");

        migrationBuilder.AddColumn<string>(
            name: "MasterAudioObjectKey",
            table: "exam_revisions",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "UsesMediaWorkspace",
            table: "exam_revisions",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }
}
