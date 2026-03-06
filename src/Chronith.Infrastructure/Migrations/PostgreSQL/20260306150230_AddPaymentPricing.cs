using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddPaymentPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AmountInCentavos",
                schema: "chronith",
                table: "bookings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                schema: "chronith",
                table: "bookings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "chronith",
                table: "bookings",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "PHP");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "chronith",
                table: "booking_types",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "PHP");

            migrationBuilder.AddColumn<long>(
                name: "PriceInCentavos",
                schema: "chronith",
                table: "booking_types",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountInCentavos",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "chronith",
                table: "booking_types");

            migrationBuilder.DropColumn(
                name: "PriceInCentavos",
                schema: "chronith",
                table: "booking_types");
        }
    }
}
