using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "photo_file_id",
                table: "attendance_punches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "work_location_id",
                table: "attendance_punches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employee_attendance_policies",
                columns: table => new
                {
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    may_clock_in_from_anywhere = table.Column<bool>(type: "boolean", nullable: false),
                    anywhere_requires_reason = table.Column<bool>(type: "boolean", nullable: false),
                    anywhere_requires_photo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_attendance_policies", x => x.employee_id);
                });

            migrationBuilder.CreateTable(
                name: "work_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    linked_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    geofence_radius_meters = table.Column<int>(type: "integer", nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_location_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_location_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_location_assignments_work_locations_work_location_id",
                        column: x => x.work_location_id,
                        principalTable: "work_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_punches_tenant_id_work_location_id",
                table: "attendance_punches",
                columns: new[] { "tenant_id", "work_location_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_attendance_policies_tenant_id_employee_id",
                table: "employee_attendance_policies",
                columns: new[] { "tenant_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_location_assignments_tenant_id_branch_id",
                table: "work_location_assignments",
                columns: new[] { "tenant_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_location_assignments_tenant_id_department_id",
                table: "work_location_assignments",
                columns: new[] { "tenant_id", "department_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_location_assignments_tenant_id_employee_id",
                table: "work_location_assignments",
                columns: new[] { "tenant_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_location_assignments_tenant_id_work_location_id_branch",
                table: "work_location_assignments",
                columns: new[] { "tenant_id", "work_location_id", "branch_id" },
                unique: true,
                filter: "scope = 'Branch'");

            migrationBuilder.CreateIndex(
                name: "ix_work_location_assignments_tenant_id_work_location_id_depart",
                table: "work_location_assignments",
                columns: new[] { "tenant_id", "work_location_id", "department_id" },
                unique: true,
                filter: "scope = 'Department'");

            migrationBuilder.CreateIndex(
                name: "ix_work_location_assignments_tenant_id_work_location_id_employ",
                table: "work_location_assignments",
                columns: new[] { "tenant_id", "work_location_id", "employee_id" },
                unique: true,
                filter: "scope = 'Employee'");

            migrationBuilder.CreateIndex(
                name: "ix_work_location_assignments_work_location_id",
                table: "work_location_assignments",
                column: "work_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_locations_tenant_id_is_active",
                table: "work_locations",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_work_locations_tenant_id_linked_branch_id",
                table: "work_locations",
                columns: new[] { "tenant_id", "linked_branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_locations_tenant_id_type",
                table: "work_locations",
                columns: new[] { "tenant_id", "type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_attendance_policies");

            migrationBuilder.DropTable(
                name: "work_location_assignments");

            migrationBuilder.DropTable(
                name: "work_locations");

            migrationBuilder.DropIndex(
                name: "ix_attendance_punches_tenant_id_work_location_id",
                table: "attendance_punches");

            migrationBuilder.DropColumn(
                name: "photo_file_id",
                table: "attendance_punches");

            migrationBuilder.DropColumn(
                name: "work_location_id",
                table: "attendance_punches");
        }
    }
}
