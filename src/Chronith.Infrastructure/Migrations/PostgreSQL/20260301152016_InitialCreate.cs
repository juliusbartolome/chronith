using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chronith");

            migrationBuilder.CreateTable(
                name: "booking_types",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    PaymentMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    BufferBeforeMinutes = table.Column<int>(type: "integer", nullable: true),
                    BufferAfterMinutes = table.Column<int>(type: "integer", nullable: true),
                    AvailableDays = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "availability_windows",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_windows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_availability_windows_booking_types_BookingTypeId",
                        column: x => x.BookingTypeId,
                        principalSchema: "chronith",
                        principalTable: "booking_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bookings_booking_types_BookingTypeId",
                        column: x => x.BookingTypeId,
                        principalSchema: "chronith",
                        principalTable: "booking_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhooks",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhooks_booking_types_BookingTypeId",
                        column: x => x.BookingTypeId,
                        principalSchema: "chronith",
                        principalTable: "booking_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_status_changes",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangedById = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangedByRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_status_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_status_changes_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "chronith",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_availability_windows_BookingTypeId",
                schema: "chronith",
                table: "availability_windows",
                column: "BookingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_status_changes_BookingId",
                schema: "chronith",
                table: "booking_status_changes",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_types_TenantId_IsDeleted",
                schema: "chronith",
                table: "booking_types",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_types_TenantId_Slug",
                schema: "chronith",
                table: "booking_types",
                columns: new[] { "TenantId", "Slug" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_BookingTypeId_Status_Start_End",
                schema: "chronith",
                table: "bookings",
                columns: new[] { "BookingTypeId", "Status", "Start", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_CustomerId",
                schema: "chronith",
                table: "bookings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_TenantId_BookingTypeId_Start_End",
                schema: "chronith",
                table: "bookings",
                columns: new[] { "TenantId", "BookingTypeId", "Start", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_TenantId_IsDeleted",
                schema: "chronith",
                table: "bookings",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                schema: "chronith",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_BookingTypeId",
                schema: "chronith",
                table: "webhooks",
                column: "BookingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_TenantId_BookingTypeId",
                schema: "chronith",
                table: "webhooks",
                columns: new[] { "TenantId", "BookingTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_TenantId_IsDeleted",
                schema: "chronith",
                table: "webhooks",
                columns: new[] { "TenantId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availability_windows",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "booking_status_changes",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "webhooks",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "bookings",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "booking_types",
                schema: "chronith");
        }
    }
}
