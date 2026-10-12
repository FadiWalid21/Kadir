using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRuleSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "statutory_rule_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    rule_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    source_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_statutory_rule_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_rule_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    source_rule_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_version = table.Column<int>(type: "integer", nullable: true),
                    is_customized = table.Column<bool>(type: "boolean", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_rule_values", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_statutory_rule_sets_country_rule_key_effective_from_version",
                table: "statutory_rule_sets",
                columns: new[] { "country", "rule_key", "effective_from", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_rule_values_tenant_id_legal_entity_id_rule_key_effec",
                table: "tenant_rule_values",
                columns: new[] { "tenant_id", "legal_entity_id", "rule_key", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_rule_values_tenant_id_rule_key_effective_from",
                table: "tenant_rule_values",
                columns: new[] { "tenant_id", "rule_key", "effective_from" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "statutory_rule_sets");

            migrationBuilder.DropTable(
                name: "tenant_rule_values");
        }
    }
}
