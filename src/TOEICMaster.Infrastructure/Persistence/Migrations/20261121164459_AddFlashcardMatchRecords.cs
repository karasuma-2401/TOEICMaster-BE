using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFlashcardMatchRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_flashcard_match_records",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                DeckId = table.Column<Guid>(type: "uuid", nullable: false),
                BestTimeMs = table.Column<int>(type: "integer", nullable: false),
                BestCombo = table.Column<int>(type: "integer", nullable: false),
                PlayCount = table.Column<int>(type: "integer", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_flashcard_match_records", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_flashcard_match_records_flashcard_decks_DeckId",
                    column: x => x.DeckId,
                    principalTable: "flashcard_decks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_flashcard_match_records_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_flashcard_match_records_DeckId",
            table: "user_flashcard_match_records",
            column: "DeckId");

        migrationBuilder.CreateIndex(
            name: "IX_user_flashcard_match_records_UserId_DeckId",
            table: "user_flashcard_match_records",
            columns: new[] { "UserId", "DeckId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_flashcard_match_records");
    }
}
