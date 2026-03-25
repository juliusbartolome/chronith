using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddPaymentRedirectUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentFailureUrl",
                schema: "chronith",
                table: "tenant_payment_configs",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentSuccessUrl",
                schema: "chronith",
                table: "tenant_payment_configs",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentFailureUrl",
                schema: "chronith",
                table: "tenant_payment_configs");

            migrationBuilder.DropColumn(
                name: "PaymentSuccessUrl",
                schema: "chronith",
                table: "tenant_payment_configs");
        }
    }
}
