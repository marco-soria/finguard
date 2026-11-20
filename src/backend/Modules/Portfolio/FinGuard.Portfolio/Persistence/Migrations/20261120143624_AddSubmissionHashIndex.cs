using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Portfolio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionHashIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_submissions_tenant_id_cut_off_date_sha256",
                schema: "portfolio",
                table: "submissions",
                columns: new[] { "tenant_id", "cut_off_date", "sha256" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_submissions_tenant_id_cut_off_date_sha256",
                schema: "portfolio",
                table: "submissions");
        }
    }
}
