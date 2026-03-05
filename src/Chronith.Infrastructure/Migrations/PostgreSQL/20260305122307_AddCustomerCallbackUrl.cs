using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddCustomerCallbackUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "WebhookId",
                schema: "chronith",
                table: "webhook_outbox_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BookingTypeId",
                schema: "chronith",
                table: "webhook_outbox_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                schema: "chronith",
                table: "webhook_outbox_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCallbackSecret",
                schema: "chronith",
                table: "booking_types",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCallbackUrl",
                schema: "chronith",
                table: "booking_types",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_outbox_entries_BookingTypeId",
                schema: "chronith",
                table: "webhook_outbox_entries",
                column: "BookingTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_outbox_entries_BookingTypeId",
                schema: "chronith",
                table: "webhook_outbox_entries");

            migrationBuilder.DropColumn(
                name: "BookingTypeId",
                schema: "chronith",
                table: "webhook_outbox_entries");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "chronith",
                table: "webhook_outbox_entries");

            migrationBuilder.DropColumn(
                name: "CustomerCallbackSecret",
                schema: "chronith",
                table: "booking_types");

            migrationBuilder.DropColumn(
                name: "CustomerCallbackUrl",
                schema: "chronith",
                table: "booking_types");

            migrationBuilder.AlterColumn<Guid>(
                name: "WebhookId",
                schema: "chronith",
                table: "webhook_outbox_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
