using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valkompass.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tier",
                table: "questions",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateIndex(
                name: "ix_questions_tier",
                table: "questions",
                column: "tier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_questions_tier",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "tier",
                table: "questions");
        }
    }
}
