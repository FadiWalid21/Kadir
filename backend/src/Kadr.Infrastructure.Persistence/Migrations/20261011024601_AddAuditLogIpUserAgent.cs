using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogIpUserAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "audit_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                table: "audit_logs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_id_action_at",
                table: "audit_logs",
                columns: new[] { "tenant_id", "action", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_id_user_id_at",
                table: "audit_logs",
                columns: new[] { "tenant_id", "user_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_tenant_id_action_at",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_tenant_id_user_id_at",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "user_agent",
                table: "audit_logs");
        }
    }
}
