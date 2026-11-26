using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Advisory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "advisory");

            migrationBuilder.CreateTable(
                name: "advisories",
                schema: "advisory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    facts = table.Column<string>(type: "jsonb", nullable: false),
                    narrative = table.Column<string>(type: "text", nullable: false),
                    narrator = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_advisories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_advisories_evaluation_id",
                schema: "advisory",
                table: "advisories",
                column: "evaluation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_advisories_source_event_id",
                schema: "advisory",
                table: "advisories",
                column: "source_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_advisories_submission_id",
                schema: "advisory",
                table: "advisories",
                column: "submission_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "advisories",
                schema: "advisory");
        }
    }
}
