using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddRecurrenceRuleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "chronith",
                table: "recurrence_rules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                schema: "chronith",
                table: "recurrence_rules",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "chronith",
                table: "recurrence_rules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StaffMemberId",
                schema: "chronith",
                table: "recurrence_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                schema: "chronith",
                table: "recurrence_rules",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_rules_TenantId_CustomerId",
                schema: "chronith",
                table: "recurrence_rules",
                columns: new[] { "TenantId", "CustomerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_recurrence_rules_TenantId_CustomerId",
                schema: "chronith",
                table: "recurrence_rules");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "chronith",
                table: "recurrence_rules");

            migrationBuilder.DropColumn(
                name: "Duration",
                schema: "chronith",
                table: "recurrence_rules");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "chronith",
                table: "recurrence_rules");

            migrationBuilder.DropColumn(
                name: "StaffMemberId",
                schema: "chronith",
                table: "recurrence_rules");

            migrationBuilder.DropColumn(
                name: "StartTime",
                schema: "chronith",
                table: "recurrence_rules");
        }
    }
}
