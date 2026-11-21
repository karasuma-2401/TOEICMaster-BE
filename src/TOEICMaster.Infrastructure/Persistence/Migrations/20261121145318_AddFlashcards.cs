using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFlashcards : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "flashcard_decks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                TargetScore = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                EstimatedMinutes = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_flashcard_decks", x => x.Id);
                table.ForeignKey(
                    name: "FK_flashcard_decks_users_OwnerUserId",
                    column: x => x.OwnerUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "flashcard_cards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DeckId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false),
                Term = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Phonetic = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PartOfSpeech = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                MeaningVi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ExampleEn = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                ExampleVi = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                collocations = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ToeicPart = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Level = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_flashcard_cards", x => x.Id);
                table.ForeignKey(
                    name: "FK_flashcard_cards_flashcard_decks_DeckId",
                    column: x => x.DeckId,
                    principalTable: "flashcard_decks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_flashcard_progress",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CardId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Starred = table.Column<bool>(type: "boolean", nullable: false),
                ReviewCount = table.Column<int>(type: "integer", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_flashcard_progress", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_flashcard_progress_flashcard_cards_CardId",
                    column: x => x.CardId,
                    principalTable: "flashcard_cards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_flashcard_progress_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_flashcard_cards_DeckId_Ordinal",
            table: "flashcard_cards",
            columns: new[] { "DeckId", "Ordinal" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_flashcard_decks_OwnerUserId_Category",
            table: "flashcard_decks",
            columns: new[] { "OwnerUserId", "Category" });

        migrationBuilder.CreateIndex(
            name: "IX_flashcard_decks_Slug",
            table: "flashcard_decks",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_flashcard_progress_CardId",
            table: "user_flashcard_progress",
            column: "CardId");

        migrationBuilder.CreateIndex(
            name: "IX_user_flashcard_progress_UserId_CardId",
            table: "user_flashcard_progress",
            columns: new[] { "UserId", "CardId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_flashcard_progress");

        migrationBuilder.DropTable(
            name: "flashcard_cards");

        migrationBuilder.DropTable(
            name: "flashcard_decks");
    }
}
