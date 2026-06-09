using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valkompass.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyLogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "party_logos",
                columns: table => new
                {
                    party_id = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_party_logos", x => x.party_id);
                    table.ForeignKey(
                        name: "fk_party_logos_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "party_logos");
        }
    }
}
