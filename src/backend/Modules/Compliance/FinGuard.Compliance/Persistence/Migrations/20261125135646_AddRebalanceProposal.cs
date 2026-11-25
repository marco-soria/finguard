using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Compliance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRebalanceProposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "proposal_status",
                schema: "compliance",
                table: "evaluations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "proposed_book_value",
                schema: "compliance",
                table: "evaluation_classes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "proposal_status",
                schema: "compliance",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "proposed_book_value",
                schema: "compliance",
                table: "evaluation_classes");
        }
    }
}
