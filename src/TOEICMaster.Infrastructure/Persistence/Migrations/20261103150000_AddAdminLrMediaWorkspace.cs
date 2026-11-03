using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

public partial class AddAdminLrMediaWorkspace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "uses_media_workspace", table: "exam_revisions", type: "boolean",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>(name: "audio_asset_id", table: "exam_blocks", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "image_required", table: "exam_questions", type: "boolean", nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "image_required", table: "exam_context_blocks", type: "boolean", nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "kind", table: "exam_media_assets", type: "character varying(16)",
            maxLength: 16, nullable: false, defaultValue: "Image");
        migrationBuilder.AddColumn<long>(name: "byte_size", table: "exam_media_assets", type: "bigint", nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "exam_media_upload_intents",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                owner_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                object_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                expected_byte_size = table.Column<long>(type: "bigint", nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_exam_media_upload_intents", x => x.id));

        migrationBuilder.CreateIndex(name: "ix_exam_blocks_audio_asset_id", table: "exam_blocks", column: "audio_asset_id");
        migrationBuilder.CreateIndex(name: "ix_exam_media_upload_intents_object_key", table: "exam_media_upload_intents",
            column: "object_key", unique: true);
        migrationBuilder.CreateIndex(name: "ix_exam_media_upload_intents_revision_id_status_expires_at_utc",
            table: "exam_media_upload_intents", columns: new[] { "revision_id", "status", "expires_at_utc" });
        migrationBuilder.AddForeignKey(name: "fk_exam_blocks_exam_media_assets_audio_asset_id", table: "exam_blocks",
            column: "audio_asset_id", principalTable: "exam_media_assets", principalColumn: "id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "fk_exam_blocks_exam_media_assets_audio_asset_id", table: "exam_blocks");
        migrationBuilder.DropTable(name: "exam_media_upload_intents");
        migrationBuilder.DropIndex(name: "ix_exam_blocks_audio_asset_id", table: "exam_blocks");
        migrationBuilder.DropColumn(name: "uses_media_workspace", table: "exam_revisions");
        migrationBuilder.DropColumn(name: "audio_asset_id", table: "exam_blocks");
        migrationBuilder.DropColumn(name: "image_required", table: "exam_questions");
        migrationBuilder.DropColumn(name: "image_required", table: "exam_context_blocks");
        migrationBuilder.DropColumn(name: "kind", table: "exam_media_assets");
        migrationBuilder.DropColumn(name: "byte_size", table: "exam_media_assets");
    }
}
