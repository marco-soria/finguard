using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Portfolio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionsReceivedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_submissions_tenant_id_received_at_id",
                schema: "portfolio",
                table: "submissions",
                columns: new[] { "tenant_id", "received_at", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_submissions_tenant_id_received_at_id",
                schema: "portfolio",
                table: "submissions");
        }
    }
}
