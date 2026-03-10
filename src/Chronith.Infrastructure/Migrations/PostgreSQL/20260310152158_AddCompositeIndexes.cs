using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_RecurrenceRuleId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_idempotency_keys_TenantId_Key_EndpointRoute",
                schema: "chronith",
                table: "idempotency_keys");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_lookup",
                schema: "chronith",
                table: "idempotency_keys",
                columns: new[] { "TenantId", "Key", "EndpointRoute" },
                unique: true);

            migrationBuilder.RenameIndex(
                name: "IX_customers_TenantId_Email",
                schema: "chronith",
                table: "customers",
                newName: "ix_customers_email");

            migrationBuilder.CreateIndex(
                name: "ix_waitlist_fifo",
                schema: "chronith",
                table: "waitlist_entries",
                columns: new[] { "TenantId", "BookingTypeId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_availability",
                schema: "chronith",
                table: "bookings",
                columns: new[] { "TenantId", "BookingTypeId", "Start", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_customer",
                schema: "chronith",
                table: "bookings",
                columns: new[] { "TenantId", "CustomerId", "Start" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_recurrence",
                schema: "chronith",
                table: "bookings",
                columns: new[] { "RecurrenceRuleId", "Start" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_staff",
                schema: "chronith",
                table: "bookings",
                columns: new[] { "TenantId", "StaffMemberId", "Start" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_waitlist_fifo",
                schema: "chronith",
                table: "waitlist_entries");

            migrationBuilder.DropIndex(
                name: "ix_bookings_availability",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_bookings_customer",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_bookings_recurrence",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_bookings_staff",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_idempotency_lookup",
                schema: "chronith",
                table: "idempotency_keys");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_TenantId_Key_EndpointRoute",
                schema: "chronith",
                table: "idempotency_keys",
                columns: new[] { "TenantId", "Key", "EndpointRoute" },
                unique: true);

            migrationBuilder.RenameIndex(
                name: "ix_customers_email",
                schema: "chronith",
                table: "customers",
                newName: "IX_customers_TenantId_Email");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_RecurrenceRuleId",
                schema: "chronith",
                table: "bookings",
                column: "RecurrenceRuleId");
        }
    }
}
