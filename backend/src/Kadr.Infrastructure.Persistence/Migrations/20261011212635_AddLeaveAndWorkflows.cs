using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveAndWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approval_workflows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_type = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_workflows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hourly_permission_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    workflow_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hourly_permission_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_balance_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    delta_days = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_balance_adjustments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    entitled = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    accrued = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    used = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    adjustment = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    carried_in = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_balances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_half_day = table.Column<bool>(type: "boolean", nullable: false),
                    days = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attachment_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    workflow_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<int>(type: "integer", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_step_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_instances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    approver_kind = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    manager_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_open = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved = table.Column<bool>(type: "boolean", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_workflow_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    approver_kind = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_final = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_workflow_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_workflow_steps_approval_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "approval_workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_workflow_steps_tenant_id_workflow_id_step_order",
                table: "approval_workflow_steps",
                columns: new[] { "tenant_id", "workflow_id", "step_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_workflow_steps_workflow_id",
                table: "approval_workflow_steps",
                column: "workflow_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_workflows_tenant_id_is_active",
                table: "approval_workflows",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_workflows_tenant_id_request_type_is_default",
                table: "approval_workflows",
                columns: new[] { "tenant_id", "request_type", "is_default" });

            migrationBuilder.CreateIndex(
                name: "ix_hourly_permission_requests_tenant_id_employee_id_status",
                table: "hourly_permission_requests",
                columns: new[] { "tenant_id", "employee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_hourly_permission_requests_tenant_id_work_date",
                table: "hourly_permission_requests",
                columns: new[] { "tenant_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_balance_adjustments_tenant_id_employee_id_year",
                table: "leave_balance_adjustments",
                columns: new[] { "tenant_id", "employee_id", "year" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_balances_tenant_id_employee_id_leave_type_id_year",
                table: "leave_balances",
                columns: new[] { "tenant_id", "employee_id", "leave_type_id", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_tenant_id_employee_id_status",
                table: "leave_requests",
                columns: new[] { "tenant_id", "employee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_tenant_id_start_date_end_date",
                table: "leave_requests",
                columns: new[] { "tenant_id", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_tenant_id_workflow_instance_id",
                table: "leave_requests",
                columns: new[] { "tenant_id", "workflow_instance_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_instances_tenant_id_request_type_status",
                table: "workflow_instances",
                columns: new[] { "tenant_id", "request_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_instances_tenant_id_subject_id",
                table: "workflow_instances",
                columns: new[] { "tenant_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_tasks_tenant_id_instance_id_step_order",
                table: "workflow_tasks",
                columns: new[] { "tenant_id", "instance_id", "step_order" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_tasks_tenant_id_is_open_assigned_user_id",
                table: "workflow_tasks",
                columns: new[] { "tenant_id", "is_open", "assigned_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_tasks_tenant_id_is_open_manager_employee_id",
                table: "workflow_tasks",
                columns: new[] { "tenant_id", "is_open", "manager_employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_tasks_tenant_id_is_open_role_id",
                table: "workflow_tasks",
                columns: new[] { "tenant_id", "is_open", "role_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_workflow_steps");

            migrationBuilder.DropTable(
                name: "hourly_permission_requests");

            migrationBuilder.DropTable(
                name: "leave_balance_adjustments");

            migrationBuilder.DropTable(
                name: "leave_balances");

            migrationBuilder.DropTable(
                name: "leave_requests");

            migrationBuilder.DropTable(
                name: "workflow_instances");

            migrationBuilder.DropTable(
                name: "workflow_tasks");

            migrationBuilder.DropTable(
                name: "approval_workflows");
        }
    }
}
