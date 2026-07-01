using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPersonalInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarBlobName",
                schema: "members",
                table: "students",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Birthdate",
                schema: "members",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdNumber",
                schema: "members",
                table: "students",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImmigrationStatus",
                schema: "members",
                table: "students",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImmigrationStatusOther",
                schema: "members",
                table: "students",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportIssuedCountry",
                schema: "members",
                table: "students",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportNumber",
                schema: "members",
                table: "students",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedIdType",
                schema: "members",
                table: "students",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VisaInterviewDate",
                schema: "members",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "members",
                table: "students",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-0000-0000-0000-000000000001"),
                columns: new[] { "AvatarBlobName", "Birthdate", "Gender", "IdNumber", "ImmigrationStatus", "ImmigrationStatusOther", "PassportIssuedCountry", "PassportNumber", "SelectedIdType", "VisaInterviewDate" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                schema: "members",
                table: "students",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-0000-0000-0000-000000000002"),
                columns: new[] { "AvatarBlobName", "Birthdate", "Gender", "IdNumber", "ImmigrationStatus", "ImmigrationStatusOther", "PassportIssuedCountry", "PassportNumber", "SelectedIdType", "VisaInterviewDate" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarBlobName",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "Birthdate",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "IdNumber",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ImmigrationStatus",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ImmigrationStatusOther",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "PassportIssuedCountry",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "PassportNumber",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "SelectedIdType",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "VisaInterviewDate",
                schema: "members",
                table: "students");
        }
    }
}
