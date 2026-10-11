using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "break_minutes",
                table: "shifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "crosses_midnight",
                table: "shifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_flexible",
                table: "shifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "out_of_range_policy",
                table: "branches",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "attendance_day_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    worked_minutes = table.Column<int>(type: "integer", nullable: false),
                    late_minutes = table.Column<int>(type: "integer", nullable: false),
                    early_leave_minutes = table.Column<int>(type: "integer", nullable: false),
                    overtime_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_manual_override = table.Column<bool>(type: "boolean", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_day_summaries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_punch_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    accuracy_m = table.Column<double>(type: "double precision", nullable: true),
                    distance_m = table.Column<double>(type: "double precision", nullable: true),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_punch_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_punches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    punched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    accuracy_m = table.Column<double>(type: "double precision", nullable: true),
                    distance_m = table.Column<double>(type: "double precision", nullable: true),
                    is_inside = table.Column<bool>(type: "boolean", nullable: false),
                    outside_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_punches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "holidays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_official = table.Column<bool>(type: "boolean", nullable: false),
                    repeats_yearly = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holidays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_day_off = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shift_assignments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_day_summaries_tenant_id_employee_id_work_date",
                table: "attendance_day_summaries",
                columns: new[] { "tenant_id", "employee_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_day_summaries_tenant_id_work_date",
                table: "attendance_day_summaries",
                columns: new[] { "tenant_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_punch_attempts_tenant_id_created_at",
                table: "attendance_punch_attempts",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_punch_attempts_tenant_id_employee_id_created_at",
                table: "attendance_punch_attempts",
                columns: new[] { "tenant_id", "employee_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_punches_tenant_id_employee_id_client_request_id",
                table: "attendance_punches",
                columns: new[] { "tenant_id", "employee_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_punches_tenant_id_employee_id_work_date",
                table: "attendance_punches",
                columns: new[] { "tenant_id", "employee_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_punches_tenant_id_work_date",
                table: "attendance_punches",
                columns: new[] { "tenant_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "ix_holidays_tenant_id_is_active",
                table: "holidays",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_holidays_tenant_id_starts_on_ends_on",
                table: "holidays",
                columns: new[] { "tenant_id", "starts_on", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "ix_shift_assignments_tenant_id_employee_id_work_date",
                table: "shift_assignments",
                columns: new[] { "tenant_id", "employee_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shift_assignments_tenant_id_work_date",
                table: "shift_assignments",
                columns: new[] { "tenant_id", "work_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_day_summaries");

            migrationBuilder.DropTable(
                name: "attendance_punch_attempts");

            migrationBuilder.DropTable(
                name: "attendance_punches");

            migrationBuilder.DropTable(
                name: "holidays");

            migrationBuilder.DropTable(
                name: "shift_assignments");

            migrationBuilder.DropColumn(
                name: "break_minutes",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "crosses_midnight",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "is_flexible",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "out_of_range_policy",
                table: "branches");
        }
    }
}
