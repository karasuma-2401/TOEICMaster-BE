using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOEICMaster.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Records when a learner moves from Listening to Reading so the server can lock Listening answers.
    /// Hand written: run <c>dotnet ef migrations add</c> in a scratch branch to compare against the generated one.
    /// </summary>
    [DbContext(typeof(ToeicDbContext))]
    [Migration("20261112150000_AddExamAttemptReadingPhase")]
    public partial class AddExamAttemptReadingPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadingStartedAtUtc",
                table: "ExamAttempts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadingStartedAtUtc",
                table: "ExamAttempts");
        }
    }
}
