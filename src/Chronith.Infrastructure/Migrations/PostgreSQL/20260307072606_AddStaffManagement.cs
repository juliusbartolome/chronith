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
            migrationBuilder.RenameColumn(
                name: "CheckoutUrl",
                schema: "chronith",
                table: "bookings",
                newName: "checkout_url");

            migrationBuilder.AlterColumn<string>(
                name: "checkout_url",
                schema: "chronith",
                table: "bookings",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StaffMemberId",
                schema: "chronith",
                table: "bookings",
                type: "uuid",
                nullable: true);

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
                name: "tenant_api_keys",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_api_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_outbox_entries",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WebhookId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_outbox_entries", x => x.Id);
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

            migrationBuilder.CreateTable(
                name: "TenantUserRefreshTokens",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUserRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUserRefreshTokens_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalSchema: "chronith",
                        principalTable: "TenantUsers",
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

            migrationBuilder.CreateIndex(
                name: "IX_tenant_api_keys_key_hash",
                schema: "chronith",
                table: "tenant_api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_api_keys_tenant_id_is_revoked",
                schema: "chronith",
                table: "tenant_api_keys",
                columns: new[] { "TenantId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserRefreshTokens_TenantUserId",
                schema: "chronith",
                table: "TenantUserRefreshTokens",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserRefreshTokens_TokenHash",
                schema: "chronith",
                table: "TenantUserRefreshTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId_Email",
                schema: "chronith",
                table: "TenantUsers",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_outbox_entries_BookingId",
                schema: "chronith",
                table: "webhook_outbox_entries",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_outbox_entries_BookingTypeId",
                schema: "chronith",
                table: "webhook_outbox_entries",
                column: "BookingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_outbox_entries_Status_NextRetryAt",
                schema: "chronith",
                table: "webhook_outbox_entries",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_outbox_entries_WebhookId",
                schema: "chronith",
                table: "webhook_outbox_entries",
                column: "WebhookId");

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
                name: "tenant_api_keys",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "TenantUserRefreshTokens",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "webhook_outbox_entries",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "staff_members",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "TenantUsers",
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
                name: "CustomerCallbackSecret",
                schema: "chronith",
                table: "booking_types");

            migrationBuilder.DropColumn(
                name: "CustomerCallbackUrl",
                schema: "chronith",
                table: "booking_types");

            migrationBuilder.DropColumn(
                name: "RequiresStaffAssignment",
                schema: "chronith",
                table: "booking_types");

            migrationBuilder.RenameColumn(
                name: "checkout_url",
                schema: "chronith",
                table: "bookings",
                newName: "CheckoutUrl");

            migrationBuilder.AlterColumn<string>(
                name: "CheckoutUrl",
                schema: "chronith",
                table: "bookings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);
        }
    }
}
