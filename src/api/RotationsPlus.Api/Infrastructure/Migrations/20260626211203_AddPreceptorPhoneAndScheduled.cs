using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreceptorPhoneAndScheduled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CallScheduled",
                schema: "marketplace",
                table: "preceptors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MobilePhone",
                schema: "marketplace",
                table: "preceptors",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "preceptors",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-0000-0000-0000-000000000001"),
                columns: new[] { "CallScheduled", "MobilePhone" },
                values: new object[] { true, "+1 312-555-0101" });

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "preceptors",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-0000-0000-0000-000000000002"),
                columns: new[] { "CallScheduled", "MobilePhone" },
                values: new object[] { false, "+1 713-555-0102" });

            migrationBuilder.InsertData(
                schema: "marketplace",
                table: "preceptors",
                columns: new[] { "Id", "Bio", "CallScheduled", "City", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Email", "FirstName", "IsDeleted", "LastName", "LicenseState", "MedicalLicenseNumber", "MobilePhone", "ModifiedAtUtc", "ModifiedBy", "PrimarySpecialtyId", "RejectionReason", "ReviewedAtUtc", "ReviewedBy", "State", "Status" },
                values: new object[] { new Guid("dddddddd-0000-0000-0000-000000000003"), null, false, "New York", new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "nadia.khan@example.com", "Nadia", false, "Khan", "NY", null, "+1 212-555-0103", null, null, new Guid("aaaaaaaa-0000-0000-0000-000000000001"), null, null, null, "NY", "Pending" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "marketplace",
                table: "preceptors",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-0000-0000-0000-000000000003"));

            migrationBuilder.DropColumn(
                name: "CallScheduled",
                schema: "marketplace",
                table: "preceptors");

            migrationBuilder.DropColumn(
                name: "MobilePhone",
                schema: "marketplace",
                table: "preceptors");
        }
    }
}
