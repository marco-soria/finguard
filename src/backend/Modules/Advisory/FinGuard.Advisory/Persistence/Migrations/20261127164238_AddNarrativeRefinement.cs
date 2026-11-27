using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Advisory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNarrativeRefinement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "model_id",
                schema: "advisory",
                table: "advisories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refinement_reason",
                schema: "advisory",
                table: "advisories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refinement_status",
                schema: "advisory",
                table: "advisories",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "NotRequested");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "model_id",
                schema: "advisory",
                table: "advisories");

            migrationBuilder.DropColumn(
                name: "refinement_reason",
                schema: "advisory",
                table: "advisories");

            migrationBuilder.DropColumn(
                name: "refinement_status",
                schema: "advisory",
                table: "advisories");
        }
    }
}
