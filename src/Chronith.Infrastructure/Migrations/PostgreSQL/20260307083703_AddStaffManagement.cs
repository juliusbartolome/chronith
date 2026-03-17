using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddStaffManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StaffMemberId",
                schema: "chronith",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresStaffAssignment",
                schema: "chronith",
                table: "booking_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "staff_members",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "booking_type_staff_assignments",
                schema: "chronith",
                columns: table => new
                {
                    BookingTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_type_staff_assignments", x => new { x.BookingTypeId, x.StaffMemberId });
                    table.ForeignKey(
                        name: "FK_booking_type_staff_assignments_booking_types_BookingTypeId",
                        column: x => x.BookingTypeId,
                        principalSchema: "chronith",
                        principalTable: "booking_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_type_staff_assignments_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalSchema: "chronith",
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_availability_windows",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_availability_windows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_availability_windows_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalSchema: "chronith",
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_StaffMemberId",
                schema: "chronith",
                table: "bookings",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_type_staff_assignments_StaffMemberId",
                schema: "chronith",
                table: "booking_type_staff_assignments",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_availability_windows_StaffMemberId",
                schema: "chronith",
                table: "staff_availability_windows",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_TenantId_IsDeleted",
                schema: "chronith",
                table: "staff_members",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_staff_members_StaffMemberId",
                schema: "chronith",
                table: "bookings",
                column: "StaffMemberId",
                principalSchema: "chronith",
                principalTable: "staff_members",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_staff_members_StaffMemberId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "booking_type_staff_assignments",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "staff_availability_windows",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "staff_members",
                schema: "chronith");

            migrationBuilder.DropIndex(
                name: "IX_bookings_StaffMemberId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "StaffMemberId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "RequiresStaffAssignment",
                schema: "chronith",
                table: "booking_types");
        }
    }
}
