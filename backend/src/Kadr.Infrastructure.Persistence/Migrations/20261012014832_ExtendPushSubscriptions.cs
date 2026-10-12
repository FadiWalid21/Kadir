using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendPushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "push_subscriptions",
                newName: "last_seen_at");

            migrationBuilder.AddColumn<string>(
                name: "device_label",
                table: "push_subscriptions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "push_subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                table: "push_subscriptions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_tenant_id_user_id_is_active",
                table: "push_subscriptions",
                columns: new[] { "tenant_id", "user_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_push_subscriptions_tenant_id_user_id_is_active",
                table: "push_subscriptions");

            migrationBuilder.DropColumn(
                name: "device_label",
                table: "push_subscriptions");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "push_subscriptions");

            migrationBuilder.DropColumn(
                name: "user_agent",
                table: "push_subscriptions");

            migrationBuilder.RenameColumn(
                name: "last_seen_at",
                table: "push_subscriptions",
                newName: "updated_at");
        }
    }
}
