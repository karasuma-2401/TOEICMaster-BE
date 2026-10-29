using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLrExerciseBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lr_exercise_bank_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Part = table.Column<short>(type: "smallint", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Topic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Difficulty = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Context = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: true),
                    AudioUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lr_exercise_bank_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lr_exercise_bank_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    OptionA = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OptionB = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OptionC = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OptionD = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CorrectKey = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    Translation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lr_exercise_bank_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lr_exercise_bank_questions_lr_exercise_bank_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "lr_exercise_bank_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lr_exercise_bank_items_Code",
                table: "lr_exercise_bank_items",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lr_exercise_bank_items_Part_Status_UpdatedAtUtc",
                table: "lr_exercise_bank_items",
                columns: new[] { "Part", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_lr_exercise_bank_questions_Code",
                table: "lr_exercise_bank_questions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lr_exercise_bank_questions_ItemId_Ordinal",
                table: "lr_exercise_bank_questions",
                columns: new[] { "ItemId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lr_exercise_bank_questions");

            migrationBuilder.DropTable(
                name: "lr_exercise_bank_items");
        }
    }
}
