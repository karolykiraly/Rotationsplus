using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentNeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomSpecialtyLocation",
                schema: "members",
                table: "students",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Importants",
                schema: "members",
                table: "students",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Interests",
                schema: "members",
                table: "students",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredSpecialty",
                schema: "members",
                table: "students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "SpecialtyLocations",
                schema: "members",
                table: "students",
                type: "text[]",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "members",
                table: "students",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-0000-0000-0000-000000000001"),
                columns: new[] { "CustomSpecialtyLocation", "Importants", "Interests", "PreferredSpecialty", "SpecialtyLocations" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                schema: "members",
                table: "students",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-0000-0000-0000-000000000002"),
                columns: new[] { "CustomSpecialtyLocation", "Importants", "Interests", "PreferredSpecialty", "SpecialtyLocations" },
                values: new object[] { null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomSpecialtyLocation",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "Importants",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "Interests",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "PreferredSpecialty",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "SpecialtyLocations",
                schema: "members",
                table: "students");
        }
    }
}
