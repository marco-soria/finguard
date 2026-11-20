using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinGuard.Compliance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLimitPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "limit_policies",
                schema: "compliance",
                columns: table => new
                {
                    version = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    cash = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    fixed_income = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    shares = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    real_estate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    alert_threshold = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    rebalance_target_ratio = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_limit_policies", x => x.version);
                    table.CheckConstraint("ck_limit_policies_alert_threshold", "alert_threshold > 0 AND alert_threshold <= 1");
                    table.CheckConstraint("ck_limit_policies_cash", "cash > 0 AND cash <= 1");
                    table.CheckConstraint("ck_limit_policies_fixed_income", "fixed_income > 0 AND fixed_income <= 1");
                    table.CheckConstraint("ck_limit_policies_limits_cover_target", "rebalance_target_ratio * (cash + fixed_income + shares + real_estate) >= 1");
                    table.CheckConstraint("ck_limit_policies_real_estate", "real_estate > 0 AND real_estate <= 1");
                    table.CheckConstraint("ck_limit_policies_rebalance_target_ratio", "rebalance_target_ratio > 0 AND rebalance_target_ratio < alert_threshold");
                    table.CheckConstraint("ck_limit_policies_shares", "shares > 0 AND shares <= 1");
                });

            migrationBuilder.InsertData(
                schema: "compliance",
                table: "limit_policies",
                columns: new[] { "version", "alert_threshold", "cash", "created_at", "effective_from", "fixed_income", "real_estate", "rebalance_target_ratio", "shares" },
                values: new object[] { 1, 0.90m, 0.40m, new DateTimeOffset(new DateTime(2026, 11, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2000, 1, 1), 0.30m, 0.25m, 0.85m, 0.25m });

            migrationBuilder.CreateIndex(
                name: "ix_limit_policies_effective_from",
                schema: "compliance",
                table: "limit_policies",
                column: "effective_from",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "limit_policies",
                schema: "compliance");
        }
    }
}
