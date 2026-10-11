using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kadr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleAuthAndNullablePassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "app_users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.CreateTable(
                name: "auth_one_time_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_one_time_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "external_logins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_logins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auth_one_time_tokens_expires_at",
                table: "auth_one_time_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_auth_one_time_tokens_purpose_token_hash",
                table: "auth_one_time_tokens",
                columns: new[] { "purpose", "token_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_logins_provider_provider_subject_user_id",
                table: "external_logins",
                columns: new[] { "provider", "provider_subject", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_logins_tenant_id_provider_provider_subject",
                table: "external_logins",
                columns: new[] { "tenant_id", "provider", "provider_subject" });

            migrationBuilder.CreateIndex(
                name: "ix_external_logins_tenant_id_user_id_provider",
                table: "external_logins",
                columns: new[] { "tenant_id", "user_id", "provider" });

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_tenant_id_token_hash",
                table: "password_reset_tokens",
                columns: new[] { "tenant_id", "token_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_tenant_id_user_id_consumed_at",
                table: "password_reset_tokens",
                columns: new[] { "tenant_id", "user_id", "consumed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_one_time_tokens");

            migrationBuilder.DropTable(
                name: "external_logins");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "app_users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
