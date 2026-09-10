using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valkompass.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddElectionForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "forecast",
                table: "election_snapshots",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "forecast",
                table: "election_snapshots");
        }
    }
}
