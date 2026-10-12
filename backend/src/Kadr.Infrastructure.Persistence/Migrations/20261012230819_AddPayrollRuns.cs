using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payroll_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    inputs_json = table.Column<string>(type: "jsonb", nullable: false),
                    gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    insurable_wage = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employee_insurance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_insurance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    taxable_income = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    income_tax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    other_deductions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_cost_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_held = table.Column<bool>(type: "boolean", nullable: false),
                    hold_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    warning_count = table.Column<int>(type: "integer", nullable: false),
                    warnings_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rules_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    reopened_from_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    // xmin is a PostgreSQL system column; mapped as concurrency token in the model only.
                    warning_count = table.Column<int>(type: "integer", nullable: false),
                    headcount = table.Column<int>(type: "integer", nullable: false),
                    total_gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    total_net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    total_employer_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_entry_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    component_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    calc_explanation_json = table.Column<string>(type: "jsonb", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_entry_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_payroll_entry_lines_payroll_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "payroll_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entries_tenant_id_employee_id",
                table: "payroll_entries",
                columns: new[] { "tenant_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entries_tenant_id_run_id_employee_id",
                table: "payroll_entries",
                columns: new[] { "tenant_id", "run_id", "employee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entries_tenant_id_run_id_is_held",
                table: "payroll_entries",
                columns: new[] { "tenant_id", "run_id", "is_held" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entries_tenant_id_run_id_warning_count",
                table: "payroll_entries",
                columns: new[] { "tenant_id", "run_id", "warning_count" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entry_lines_entry_id",
                table: "payroll_entry_lines",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entry_lines_tenant_id_entry_id_line_type",
                table: "payroll_entry_lines",
                columns: new[] { "tenant_id", "entry_id", "line_type" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_entry_lines_tenant_id_entry_id_sort_order",
                table: "payroll_entry_lines",
                columns: new[] { "tenant_id", "entry_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_runs_tenant_id_legal_entity_id_period_end",
                table: "payroll_runs",
                columns: new[] { "tenant_id", "legal_entity_id", "period_end" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_runs_tenant_id_period_start_period_end",
                table: "payroll_runs",
                columns: new[] { "tenant_id", "period_start", "period_end" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_runs_tenant_id_reopened_from_run_id",
                table: "payroll_runs",
                columns: new[] { "tenant_id", "reopened_from_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_runs_tenant_id_status",
                table: "payroll_runs",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payroll_entry_lines");

            migrationBuilder.DropTable(
                name: "payroll_runs");

            migrationBuilder.DropTable(
                name: "payroll_entries");
        }
    }
}
