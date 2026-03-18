using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddTenantPaymentConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_payment_configs",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Settings = table.Column<string>(type: "text", nullable: false),
                    PublicNote = table.Column<string>(type: "text", nullable: true),
                    QrCodeUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_payment_configs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payment_configs_TenantId",
                schema: "chronith",
                table: "tenant_payment_configs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payment_configs_TenantId_ProviderName_active",
                schema: "chronith",
                table: "tenant_payment_configs",
                columns: new[] { "TenantId", "ProviderName" },
                unique: true,
                filter: "\"IsActive\" = true AND \"IsDeleted\" = false AND \"ProviderName\" != 'Manual'");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payment_configs_TenantId_ProviderName_Label",
                schema: "chronith",
                table: "tenant_payment_configs",
                columns: new[] { "TenantId", "ProviderName", "Label" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_payment_configs",
                schema: "chronith");
        }
    }
}
