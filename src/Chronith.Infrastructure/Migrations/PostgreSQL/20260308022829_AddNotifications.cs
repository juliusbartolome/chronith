using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReminderIntervals",
                schema: "chronith",
                table: "booking_types",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_reminders",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_notification_configs",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_notification_configs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_reminders_BookingId",
                schema: "chronith",
                table: "booking_reminders",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_reminders_BookingId_IntervalMinutes",
                schema: "chronith",
                table: "booking_reminders",
                columns: new[] { "BookingId", "IntervalMinutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_notification_configs_TenantId",
                schema: "chronith",
                table: "tenant_notification_configs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_notification_configs_TenantId_ChannelType",
                schema: "chronith",
                table: "tenant_notification_configs",
                columns: new[] { "TenantId", "ChannelType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_reminders",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "tenant_notification_configs",
                schema: "chronith");

            migrationBuilder.DropColumn(
                name: "ReminderIntervals",
                schema: "chronith",
                table: "booking_types");
        }
    }
}
