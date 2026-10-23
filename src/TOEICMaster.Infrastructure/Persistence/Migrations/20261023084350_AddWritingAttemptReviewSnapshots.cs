using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingAttemptReviewSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CriterionScore",
                table: "writing_evaluation_feedback",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewSnapshotJson",
                table: "writing_attempt_questions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriterionScore",
                table: "writing_evaluation_feedback");

            migrationBuilder.DropColumn(
                name: "ReviewSnapshotJson",
                table: "writing_attempt_questions");
        }
    }
}
