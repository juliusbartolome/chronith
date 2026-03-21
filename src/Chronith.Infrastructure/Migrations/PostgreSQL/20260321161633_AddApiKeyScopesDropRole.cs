using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddApiKeyScopesDropRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                schema: "chronith",
                table: "tenant_api_keys");

            migrationBuilder.AddColumn<List<string>>(
                name: "scopes",
                schema: "chronith",
                table: "tenant_api_keys",
                type: "text[]",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scopes",
                schema: "chronith",
                table: "tenant_api_keys");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "chronith",
                table: "tenant_api_keys",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
