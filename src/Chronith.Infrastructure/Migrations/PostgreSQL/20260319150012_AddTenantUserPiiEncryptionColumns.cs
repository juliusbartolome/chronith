using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddTenantUserPiiEncryptionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailEncrypted",
                schema: "chronith",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailToken",
                schema: "chronith",
                table: "customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneEncrypted",
                schema: "chronith",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_email_token",
                schema: "chronith",
                table: "customers",
                columns: new[] { "TenantId", "EmailToken" },
                filter: "\"EmailToken\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_customers_email_token",
                schema: "chronith",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "EmailEncrypted",
                schema: "chronith",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "EmailToken",
                schema: "chronith",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "PhoneEncrypted",
                schema: "chronith",
                table: "customers");
        }
    }
}
