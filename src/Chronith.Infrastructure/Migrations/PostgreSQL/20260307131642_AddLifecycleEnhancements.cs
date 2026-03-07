using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddLifecycleEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFields",
                schema: "chronith",
                table: "bookings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldSchema",
                schema: "chronith",
                table: "booking_types",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "time_blocks",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_blocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "waitlist_entries",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DesiredStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DesiredEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OfferedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waitlist_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_time_blocks_TenantId_IsDeleted",
                schema: "chronith",
                table: "time_blocks",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_time_blocks_TenantId_Start_End",
                schema: "chronith",
                table: "time_blocks",
                columns: new[] { "TenantId", "Start", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_TenantId_BookingTypeId_Status",
                schema: "chronith",
                table: "waitlist_entries",
                columns: new[] { "TenantId", "BookingTypeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_TenantId_IsDeleted",
                schema: "chronith",
                table: "waitlist_entries",
                columns: new[] { "TenantId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "time_blocks",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "waitlist_entries",
                schema: "chronith");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CustomFieldSchema",
                schema: "chronith",
                table: "booking_types");
        }
    }
}
