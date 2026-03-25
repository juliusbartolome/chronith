using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronith.Infrastructure.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddBookingContactFieldsRefactorCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Customer: split Name → FirstName + LastName ─────────────────

            // 1. Add FirstName column
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "chronith",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // 2. Populate FirstName/LastName from existing Name column
            //    Split on first space: "Julius Bartolome" → FirstName="Julius", LastName="Bartolome"
            //    Single names go entirely to FirstName, LastName stays empty string
            migrationBuilder.Sql("""
                UPDATE chronith.customers
                SET "FirstName" = CASE
                        WHEN position(' ' in "Name") > 0
                            THEN left("Name", position(' ' in "Name") - 1)
                        ELSE "Name"
                    END;
                """);

            // 3. Rename Name → LastName (keeps existing data temporarily)
            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "chronith",
                table: "customers",
                newName: "LastName");

            // 4. Update LastName to only contain the last-name portion
            migrationBuilder.Sql("""
                UPDATE chronith.customers
                SET "LastName" = CASE
                        WHEN position(' ' in "FirstName" || ' ' || "LastName") > 0
                             AND length("LastName") > length("FirstName")
                            THEN substring("LastName" from position(' ' in "LastName") + 1)
                        WHEN "FirstName" = "LastName"
                            THEN ''
                        ELSE "LastName"
                    END;
                """);

            // ── Customer: rename Phone → Mobile, PhoneEncrypted → MobileEncrypted ──

            migrationBuilder.RenameColumn(
                name: "PhoneEncrypted",
                schema: "chronith",
                table: "customers",
                newName: "MobileEncrypted");

            migrationBuilder.RenameColumn(
                name: "Phone",
                schema: "chronith",
                table: "customers",
                newName: "Mobile");

            // ── Booking: add FirstName, LastName, Mobile columns ────────────

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "chronith",
                table: "bookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "chronith",
                table: "bookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                schema: "chronith",
                table: "bookings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Booking: drop new columns ───────────────────────────────────

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "chronith",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Mobile",
                schema: "chronith",
                table: "bookings");

            // ── Customer: merge FirstName + LastName → Name ─────────────────

            // Rename LastName → Name, then merge FirstName into it
            migrationBuilder.RenameColumn(
                name: "LastName",
                schema: "chronith",
                table: "customers",
                newName: "Name");

            migrationBuilder.Sql("""
                UPDATE chronith.customers
                SET "Name" = CASE
                        WHEN length("FirstName") > 0 AND length("Name") > 0
                            THEN "FirstName" || ' ' || "Name"
                        WHEN length("FirstName") > 0
                            THEN "FirstName"
                        ELSE "Name"
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "chronith",
                table: "customers");

            // ── Customer: rename Mobile → Phone, MobileEncrypted → PhoneEncrypted ──

            migrationBuilder.RenameColumn(
                name: "MobileEncrypted",
                schema: "chronith",
                table: "customers",
                newName: "PhoneEncrypted");

            migrationBuilder.RenameColumn(
                name: "Mobile",
                schema: "chronith",
                table: "customers",
                newName: "Phone");
        }
    }
}
