using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddWebhookEventSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_event_subscriptions",
                schema: "chronith",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WebhookId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_event_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_event_subscriptions_webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalSchema: "chronith",
                        principalTable: "webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_event_subscriptions_WebhookId_EventName",
                schema: "chronith",
                table: "webhook_event_subscriptions",
                columns: new[] { "WebhookId", "EventName" },
                unique: true);

            // Seed existing webhooks with all 4 event types for backward compatibility
            migrationBuilder.Sql("""
                INSERT INTO chronith.webhook_event_subscriptions ("Id", "WebhookId", "EventName")
                SELECT gen_random_uuid(), w."Id", e."EventName"
                FROM chronith.webhooks w
                CROSS JOIN (VALUES
                    ('booking.payment_received'),
                    ('booking.confirmed'),
                    ('booking.cancelled'),
                    ('booking.payment_failed')
                ) AS e("EventName")
                WHERE NOT w."IsDeleted"
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_event_subscriptions",
                schema: "chronith");
        }
    }
}
