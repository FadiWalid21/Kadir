using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BranchNameArEnAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                table: "branches",
                newName: "name_en");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "branches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_ar",
                table: "branches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "search_name",
                table: "branches",
                type: "character varying(420)",
                maxLength: 420,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE branches
                SET name_ar = name_en,
                    search_name = lower(trim(both from coalesce(name_en, '') || ' ' || coalesce(name_en, '')));
                """);

            migrationBuilder.CreateIndex(
                name: "ix_branches_tenant_id_is_active",
                table: "branches",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_branches_tenant_id_search_name",
                table: "branches",
                columns: new[] { "tenant_id", "search_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_branches_tenant_id_is_active",
                table: "branches");

            migrationBuilder.DropIndex(
                name: "ix_branches_tenant_id_search_name",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "address",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "name_ar",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "search_name",
                table: "branches");

            migrationBuilder.RenameColumn(
                name: "name_en",
                table: "branches",
                newName: "name");
        }
    }
}
