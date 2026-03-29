using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddBookingProofFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentNote",
                schema: "chronith",
                table: "bookings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofOfPaymentFileName",
                schema: "chronith",
                table: "bookings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofOfPaymentUrl",
                schema: "chronith",
                table: "bookings",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentNote",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "ProofOfPaymentFileName",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "ProofOfPaymentUrl",
                schema: "chronith",
                table: "bookings");
        }
    }
}
