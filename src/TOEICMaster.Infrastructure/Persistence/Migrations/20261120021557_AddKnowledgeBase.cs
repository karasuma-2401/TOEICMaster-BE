using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddKnowledgeBase : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "knowledge_categories",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_categories", x => x.id);
                table.CheckConstraint("ck_knowledge_categories_display_order", "display_order >= 0");
                table.CheckConstraint("ck_knowledge_categories_slug_lower", "slug = lower(slug)");
                table.ForeignKey(
                    name: "FK_knowledge_categories_users_created_by_user_id",
                    column: x => x.created_by_user_id,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_knowledge_categories_users_updated_by_user_id",
                    column: x => x.updated_by_user_id,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "knowledge_media_assets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                object_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                byte_length = table.Column<long>(type: "bigint", nullable: false),
                sha256 = table.Column<string>(type: "character(64)", nullable: false),
                uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_media_assets", x => x.id);
                table.CheckConstraint("ck_knowledge_media_assets_byte_length", "byte_length > 0 AND byte_length <= 5242880");
                table.ForeignKey(
                    name: "FK_knowledge_media_assets_users_uploaded_by_user_id",
                    column: x => x.uploaded_by_user_id,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "knowledge_tags",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_tags", x => x.id);
                table.CheckConstraint("ck_knowledge_tags_slug_lower", "slug = lower(slug)");
            });

        migrationBuilder.CreateTable(
            name: "knowledge_articles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                skill = table.Column<short>(type: "smallint", nullable: false),
                category_id = table.Column<Guid>(type: "uuid", nullable: false),
                cover_image_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                cover_image_alt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                content_json = table.Column<string>(type: "jsonb", nullable: false),
                toc_json = table.Column<string>(type: "jsonb", nullable: false),
                estimated_read_minutes = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_articles", x => x.id);
                table.CheckConstraint("ck_knowledge_articles_published_at", "status <> 2 OR published_at_utc IS NOT NULL");
                table.CheckConstraint("ck_knowledge_articles_read_minutes", "estimated_read_minutes >= 1");
                table.CheckConstraint("ck_knowledge_articles_skill", "skill BETWEEN 0 AND 4");
                table.CheckConstraint("ck_knowledge_articles_slug_lower", "slug = lower(slug)");
                table.CheckConstraint("ck_knowledge_articles_status", "status BETWEEN 1 AND 3");
                table.ForeignKey(
                    name: "FK_knowledge_articles_knowledge_categories_category_id",
                    column: x => x.category_id,
                    principalTable: "knowledge_categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_knowledge_articles_knowledge_media_assets_cover_image_asset~",
                    column: x => x.cover_image_asset_id,
                    principalTable: "knowledge_media_assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_knowledge_articles_users_created_by_user_id",
                    column: x => x.created_by_user_id,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_knowledge_articles_users_updated_by_user_id",
                    column: x => x.updated_by_user_id,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "knowledge_article_media_assets",
            columns: table => new
            {
                article_id = table.Column<Guid>(type: "uuid", nullable: false),
                media_asset_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_article_media_assets", x => new { x.article_id, x.media_asset_id });
                table.ForeignKey(
                    name: "FK_knowledge_article_media_assets_knowledge_articles_article_id",
                    column: x => x.article_id,
                    principalTable: "knowledge_articles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_knowledge_article_media_assets_knowledge_media_assets_media~",
                    column: x => x.media_asset_id,
                    principalTable: "knowledge_media_assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "knowledge_article_tags",
            columns: table => new
            {
                article_id = table.Column<Guid>(type: "uuid", nullable: false),
                tag_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_article_tags", x => new { x.article_id, x.tag_id });
                table.ForeignKey(
                    name: "FK_knowledge_article_tags_knowledge_articles_article_id",
                    column: x => x.article_id,
                    principalTable: "knowledge_articles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_knowledge_article_tags_knowledge_tags_tag_id",
                    column: x => x.tag_id,
                    principalTable: "knowledge_tags",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_knowledge_article_media_assets_media_asset_id",
            table: "knowledge_article_media_assets",
            column: "media_asset_id");

        migrationBuilder.CreateIndex(
            name: "ix_knowledge_article_tags_tag_id",
            table: "knowledge_article_tags",
            column: "tag_id");

        migrationBuilder.CreateIndex(
            name: "ix_knowledge_articles_category_id",
            table: "knowledge_articles",
            column: "category_id");

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_articles_cover_image_asset_id",
            table: "knowledge_articles",
            column: "cover_image_asset_id");

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_articles_created_by_user_id",
            table: "knowledge_articles",
            column: "created_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_knowledge_articles_status_skill_category_published",
            table: "knowledge_articles",
            columns: new[] { "status", "skill", "category_id", "published_at_utc" },
            descending: new[] { false, false, false, true });

        migrationBuilder.CreateIndex(
            name: "ix_knowledge_articles_status_updated",
            table: "knowledge_articles",
            columns: new[] { "status", "updated_at_utc" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_articles_updated_by_user_id",
            table: "knowledge_articles",
            column: "updated_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ux_knowledge_articles_slug",
            table: "knowledge_articles",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_categories_created_by_user_id",
            table: "knowledge_categories",
            column: "created_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_knowledge_categories_display_order_name",
            table: "knowledge_categories",
            columns: new[] { "display_order", "name" });

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_categories_updated_by_user_id",
            table: "knowledge_categories",
            column: "updated_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ux_knowledge_categories_slug",
            table: "knowledge_categories",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_media_assets_uploaded_by_user_id",
            table: "knowledge_media_assets",
            column: "uploaded_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ux_knowledge_media_assets_object_key",
            table: "knowledge_media_assets",
            column: "object_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_knowledge_tags_slug",
            table: "knowledge_tags",
            column: "slug",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "knowledge_article_media_assets");

        migrationBuilder.DropTable(
            name: "knowledge_article_tags");

        migrationBuilder.DropTable(
            name: "knowledge_articles");

        migrationBuilder.DropTable(
            name: "knowledge_tags");

        migrationBuilder.DropTable(
            name: "knowledge_categories");

        migrationBuilder.DropTable(
            name: "knowledge_media_assets");
    }
}
