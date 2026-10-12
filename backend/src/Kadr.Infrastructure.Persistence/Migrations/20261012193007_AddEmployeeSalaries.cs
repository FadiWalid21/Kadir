using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeSalaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_salaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    basic_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    change_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_salaries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "employee_salary_components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_salary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_or_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_salary_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_salary_components_employee_salaries_employee_salar",
                        column: x => x.employee_salary_id,
                        principalTable: "employee_salaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_salaries_tenant_id_employee_id",
                table: "employee_salaries",
                columns: new[] { "tenant_id", "employee_id" },
                unique: true,
                filter: "effective_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employee_salaries_tenant_id_employee_id_effective_from",
                table: "employee_salaries",
                columns: new[] { "tenant_id", "employee_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_salaries_tenant_id_employee_id_effective_to",
                table: "employee_salaries",
                columns: new[] { "tenant_id", "employee_id", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_salary_components_employee_salary_id",
                table: "employee_salary_components",
                column: "employee_salary_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_salary_components_tenant_id_employee_salary_id_pay",
                table: "employee_salary_components",
                columns: new[] { "tenant_id", "employee_salary_id", "pay_component_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_salary_components_tenant_id_pay_component_id",
                table: "employee_salary_components",
                columns: new[] { "tenant_id", "pay_component_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_salary_components");

            migrationBuilder.DropTable(
                name: "employee_salaries");
        }
    }
}
