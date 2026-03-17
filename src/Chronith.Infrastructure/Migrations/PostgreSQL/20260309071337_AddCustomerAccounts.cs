using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddCustomerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerAccountId",
                schema: "chronith",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurrenceRuleId",
                schema: "chronith",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AuthProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EndpointRoute = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", maxLength: 65536, nullable: false),
                    ResponseContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recurrence_rules",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "jsonb", nullable: true),
                    SeriesStart = table.Column<DateOnly>(type: "date", nullable: false),
                    SeriesEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    MaxOccurrences = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurrence_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recurrence_rules_booking_types_BookingTypeId",
                        column: x => x.BookingTypeId,
                        principalSchema: "chronith",
                        principalTable: "booking_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_auth_configs",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowBuiltInAuth = table.Column<bool>(type: "boolean", nullable: false),
                    OidcIssuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OidcClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OidcAudience = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MagicLinkEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_auth_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_refresh_tokens",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_refresh_tokens_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "chronith",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_CustomerAccountId",
                schema: "chronith",
                table: "bookings",
                column: "CustomerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_RecurrenceRuleId",
                schema: "chronith",
                table: "bookings",
                column: "RecurrenceRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_refresh_tokens_CustomerId",
                schema: "chronith",
                table: "customer_refresh_tokens",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_refresh_tokens_TokenHash",
                schema: "chronith",
                table: "customer_refresh_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Email",
                schema: "chronith",
                table: "customers",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_ExternalId",
                schema: "chronith",
                table: "customers",
                columns: new[] { "TenantId", "ExternalId" },
                filter: "\"ExternalId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_IsDeleted",
                schema: "chronith",
                table: "customers",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_ExpiresAt",
                schema: "chronith",
                table: "idempotency_keys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_TenantId_Key_EndpointRoute",
                schema: "chronith",
                table: "idempotency_keys",
                columns: new[] { "TenantId", "Key", "EndpointRoute" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_rules_BookingTypeId",
                schema: "chronith",
                table: "recurrence_rules",
                column: "BookingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_rules_TenantId_IsDeleted",
                schema: "chronith",
                table: "recurrence_rules",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_auth_configs_TenantId",
                schema: "chronith",
                table: "tenant_auth_configs",
                column: "TenantId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_customers_CustomerAccountId",
                schema: "chronith",
                table: "bookings",
                column: "CustomerAccountId",
                principalSchema: "chronith",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_recurrence_rules_RecurrenceRuleId",
                schema: "chronith",
                table: "bookings",
                column: "RecurrenceRuleId",
                principalSchema: "chronith",
                principalTable: "recurrence_rules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_customers_CustomerAccountId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_bookings_recurrence_rules_RecurrenceRuleId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "customer_refresh_tokens",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "recurrence_rules",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "tenant_auth_configs",
                schema: "chronith");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "chronith");

            migrationBuilder.DropIndex(
                name: "IX_bookings_CustomerAccountId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_RecurrenceRuleId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CustomerAccountId",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "RecurrenceRuleId",
                schema: "chronith",
                table: "bookings");
        }
    }
}
