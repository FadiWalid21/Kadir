using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeInsuranceProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_insurance_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    insurance_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    insurable_wage_override = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_insured = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_insurance_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_insurance_profiles_tenant_id_employee_id",
                table: "employee_insurance_profiles",
                columns: new[] { "tenant_id", "employee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_insurance_profiles_tenant_id_is_insured",
                table: "employee_insurance_profiles",
                columns: new[] { "tenant_id", "is_insured" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_insurance_profiles");
        }
    }
}
