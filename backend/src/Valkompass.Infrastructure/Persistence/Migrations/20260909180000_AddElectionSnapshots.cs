using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Valkompass.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddElectionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "election_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    stage = table.Column<int>(type: "integer", nullable: false),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_test = table.Column<bool>(type: "boolean", nullable: false),
                    source_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    districts_reported = table.Column<int>(type: "integer", nullable: false),
                    districts_total = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_election_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_election_snapshots_stage_checksum",
                table: "election_snapshots",
                columns: new[] { "stage", "checksum" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_election_snapshots_stage_ingested_at",
                table: "election_snapshots",
                columns: new[] { "stage", "ingested_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "election_snapshots");
        }
    }
}
