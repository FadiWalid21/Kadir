using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionTrialEndsAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_subscriptions_tenant_id",
                table: "subscriptions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trial_ends_at",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE subscriptions
                SET trial_ends_at = renews_at
                WHERE trial_ends_at IS NULL AND renews_at IS NOT NULL AND status = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_id",
                table: "subscriptions",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_subscriptions_tenant_id",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "trial_ends_at",
                table: "subscriptions");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_id",
                table: "subscriptions",
                column: "tenant_id");
        }
    }
}
