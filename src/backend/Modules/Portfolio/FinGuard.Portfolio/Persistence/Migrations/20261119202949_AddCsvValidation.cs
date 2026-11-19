using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Portfolio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCsvValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "error_count",
                schema: "portfolio",
                table: "submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "row_count",
                schema: "portfolio",
                table: "submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "positions",
                schema: "portfolio",
                columns: table => new
                {
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asset_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    market_value_usd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => new { x.submission_id, x.row_number });
                    table.ForeignKey(
                        name: "fk_positions_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "portfolio",
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_errors",
                schema: "portfolio",
                columns: table => new
                {
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: true),
                    column = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_errors", x => new { x.submission_id, x.ordinal });
                    table.ForeignKey(
                        name: "fk_submission_errors_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "portfolio",
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Phase 1 rows were stored before validation existed (D-117): they become Rejected with one file error.
            // Insert first, then update, so a re-run inside the migration finds nothing left to change.
            migrationBuilder.Sql("""
                INSERT INTO portfolio.submission_errors
                    (submission_id, ordinal, tenant_id, row_number, "column", code, message)
                SELECT id, 0, tenant_id, NULL, NULL, 'Portfolio.Csv.NotValidated', 'Uploaded before validation existed.'
                FROM portfolio.submissions
                WHERE status = 'Received'
                ON CONFLICT (submission_id, ordinal) DO NOTHING;

                UPDATE portfolio.submissions
                SET status = 'Rejected', error_count = 1
                WHERE status = 'Received';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE portfolio.submissions
                SET status = 'Received'
                WHERE id IN (SELECT submission_id FROM portfolio.submission_errors WHERE code = 'Portfolio.Csv.NotValidated');
                """);

            migrationBuilder.DropTable(
                name: "positions",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "submission_errors",
                schema: "portfolio");

            migrationBuilder.DropColumn(
                name: "error_count",
                schema: "portfolio",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "row_count",
                schema: "portfolio",
                table: "submissions");
        }
    }
}
