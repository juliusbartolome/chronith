using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class ExpandEmailColumnsForEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailEncrypted",
                schema: "chronith",
                table: "TenantUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailToken",
                schema: "chronith",
                table: "TenantUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenantusers_email_token",
                schema: "chronith",
                table: "TenantUsers",
                columns: new[] { "TenantId", "EmailToken" },
                filter: "\"EmailToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenantusers_email_token",
                schema: "chronith",
                table: "TenantUsers");

            migrationBuilder.DropColumn(
                name: "EmailEncrypted",
                schema: "chronith",
                table: "TenantUsers");

            migrationBuilder.DropColumn(
                name: "EmailToken",
                schema: "chronith",
                table: "TenantUsers");
        }
    }
}
