using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "marketplace",
                table: "programs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgramNumber",
                schema: "marketplace",
                table: "programs",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "marketplace",
                table: "programs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                schema: "marketplace",
                table: "programs",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000001"),
                columns: new[] { "City", "ProgramNumber", "State", "Tags" },
                values: new object[] { "Los Angeles", 1001, "CA", new List<string> { "Hospital Letterhead LOR", "Inpatient" } });

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000002"),
                columns: new[] { "City", "ProgramNumber", "State", "Tags" },
                values: new object[] { "Remote", 1002, "NY", new List<string> { "Research" } });

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000003"),
                columns: new[] { "City", "ProgramNumber", "State", "Tags" },
                values: new object[] { "Houston", 1003, "TX", new List<string> { "Hands On" } });

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000004"),
                columns: new[] { "City", "ProgramNumber", "State", "Tags" },
                values: new object[] { "Chicago", 1004, "IL", new List<string>() });

            migrationBuilder.CreateIndex(
                name: "IX_programs_ProgramNumber",
                schema: "marketplace",
                table: "programs",
                column: "ProgramNumber",
                unique: true);

            // Adding the identity column back-fills existing rows from the sequence (low values), and the
            // seeded rows are set explicitly to 1001-1004. Restart the identity sequence well above both so
            // server-assigned numbers for new programs never collide with seeded or back-filled values.
            // (Quoted column identifier — NOT pg_get_serial_sequence, which down-cases its column arg and
            // would miss "ProgramNumber".) The production data migration carries legacy program_id into
            // ProgramNumber and bumps the sequence past that max at cutover (see Deployment_Log #47).
            migrationBuilder.Sql("ALTER TABLE marketplace.programs ALTER COLUMN \"ProgramNumber\" RESTART WITH 2000;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_programs_ProgramNumber",
                schema: "marketplace",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "marketplace",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "ProgramNumber",
                schema: "marketplace",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "marketplace",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "Tags",
                schema: "marketplace",
                table: "programs");
        }
    }
}
