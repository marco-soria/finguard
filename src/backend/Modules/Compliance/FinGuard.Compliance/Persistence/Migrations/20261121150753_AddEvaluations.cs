using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Compliance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluations",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cut_off_date = table.Column<DateOnly>(type: "date", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_book_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    overall_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    policy_version = table.Column<int>(type: "integer", nullable: false),
                    cash_limit = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    fixed_income_limit = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    shares_limit = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    real_estate_limit = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    alert_threshold = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    rebalance_target_ratio = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_classes",
                schema: "compliance",
                columns: table => new
                {
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_class = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    share = table.Column<decimal>(type: "numeric", nullable: false),
                    utilization = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_classes", x => new { x.evaluation_id, x.asset_class });
                    table.ForeignKey(
                        name: "fk_evaluation_classes_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalSchema: "compliance",
                        principalTable: "evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evaluations_submission_id",
                schema: "compliance",
                table: "evaluations",
                column: "submission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluations_tenant_id_cut_off_date",
                schema: "compliance",
                table: "evaluations",
                columns: new[] { "tenant_id", "cut_off_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_classes",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "evaluations",
                schema: "compliance");
        }
    }
}
