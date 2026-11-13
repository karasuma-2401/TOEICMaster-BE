using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

public partial class AddAdminLrMediaWorkspace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "UsesMediaWorkspace", table: "exam_revisions", type: "boolean",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>(name: "AudioAssetId", table: "exam_blocks", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "ImageRequired", table: "exam_questions", type: "boolean", nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "ImageRequired", table: "exam_context_blocks", type: "boolean", nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "Kind", table: "exam_media_assets", type: "character varying(16)",
            maxLength: 16, nullable: false, defaultValue: "Image");
        migrationBuilder.AddColumn<long>(name: "ByteSize", table: "exam_media_assets", type: "bigint", nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "exam_media_upload_intents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                OwnerKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ObjectKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ExpectedByteSize = table.Column<long>(type: "bigint", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_exam_media_upload_intents", x => x.Id));

        migrationBuilder.CreateIndex(name: "ix_exam_blocks_audio_asset_id", table: "exam_blocks", column: "AudioAssetId");
        migrationBuilder.CreateIndex(name: "ix_exam_media_upload_intents_object_key", table: "exam_media_upload_intents",
            column: "ObjectKey", unique: true);
        migrationBuilder.CreateIndex(name: "ix_exam_media_upload_intents_revision_id_status_expires_at_utc",
            table: "exam_media_upload_intents", columns: new[] { "RevisionId", "Status", "ExpiresAtUtc" });
        migrationBuilder.AddForeignKey(name: "fk_exam_blocks_exam_media_assets_audio_asset_id", table: "exam_blocks",
            column: "AudioAssetId", principalTable: "exam_media_assets", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "fk_exam_blocks_exam_media_assets_audio_asset_id", table: "exam_blocks");
        migrationBuilder.DropTable(name: "exam_media_upload_intents");
        migrationBuilder.DropIndex(name: "ix_exam_blocks_audio_asset_id", table: "exam_blocks");
        migrationBuilder.DropColumn(name: "UsesMediaWorkspace", table: "exam_revisions");
        migrationBuilder.DropColumn(name: "AudioAssetId", table: "exam_blocks");
        migrationBuilder.DropColumn(name: "ImageRequired", table: "exam_questions");
        migrationBuilder.DropColumn(name: "ImageRequired", table: "exam_context_blocks");
        migrationBuilder.DropColumn(name: "Kind", table: "exam_media_assets");
        migrationBuilder.DropColumn(name: "ByteSize", table: "exam_media_assets");
    }
}
