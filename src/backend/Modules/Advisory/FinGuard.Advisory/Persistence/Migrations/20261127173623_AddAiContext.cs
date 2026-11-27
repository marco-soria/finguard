using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Advisory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_context",
                schema: "advisory",
                table: "advisories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_context",
                schema: "advisory",
                table: "advisories");
        }
    }
}
