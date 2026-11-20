using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Compliance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compliance");

            migrationBuilder.CreateTable(
                name: "portfolio_snapshots",
                schema: "compliance",
                columns: table => new
                {
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cut_off_date = table.Column<DateOnly>(type: "date", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fixed_income = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    shares = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    real_estate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_snapshots", x => x.submission_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_snapshots_source_event_id",
                schema: "compliance",
                table: "portfolio_snapshots",
                column: "source_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_snapshots_tenant_id_cut_off_date_received_at",
                schema: "compliance",
                table: "portfolio_snapshots",
                columns: new[] { "tenant_id", "cut_off_date", "received_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_snapshots",
                schema: "compliance");
        }
    }
}
