using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Valkompass.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBarometer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pollsters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    commissioner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pollsters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "polls",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_key = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    pollster_id = table.Column<int>(type: "integer", nullable: false),
                    field_start = table.Column<DateOnly>(type: "date", nullable: true),
                    field_end = table.Column<DateOnly>(type: "date", nullable: true),
                    published_at = table.Column<DateOnly>(type: "date", nullable: false),
                    sample_size = table.Column<int>(type: "integer", nullable: true),
                    source_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    source_citation = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_polls", x => x.id);
                    table.ForeignKey(
                        name: "fk_polls_pollsters_pollster_id",
                        column: x => x.pollster_id,
                        principalTable: "pollsters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    poll_id = table.Column<int>(type: "integer", nullable: false),
                    party_id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: true),
                    margin_of_error = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poll_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_poll_results_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_poll_results_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_poll_results_party_id",
                table: "poll_results",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_poll_results_poll_id_party_id",
                table: "poll_results",
                columns: new[] { "poll_id", "party_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_polls_external_key",
                table: "polls",
                column: "external_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_polls_pollster_id",
                table: "polls",
                column: "pollster_id");

            migrationBuilder.CreateIndex(
                name: "ix_polls_published_at",
                table: "polls",
                column: "published_at");

            migrationBuilder.CreateIndex(
                name: "ix_pollsters_code",
                table: "pollsters",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poll_results");

            migrationBuilder.DropTable(
                name: "polls");

            migrationBuilder.DropTable(
                name: "pollsters");
        }
    }
}
