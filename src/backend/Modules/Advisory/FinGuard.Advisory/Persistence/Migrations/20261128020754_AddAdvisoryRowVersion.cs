using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Advisory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisoryRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "advisory",
                table: "advisories",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "advisory",
                table: "advisories");
        }
    }
}
