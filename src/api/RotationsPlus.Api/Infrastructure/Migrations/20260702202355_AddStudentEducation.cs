using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentEducation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppliedMatch",
                schema: "members",
                table: "students",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Association",
                schema: "members",
                table: "students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ComlexLevel1Passed",
                schema: "members",
                table: "students",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ComlexLevel1Taken",
                schema: "members",
                table: "students",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComlexLevel2",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComlexLevel2Attempts",
                schema: "members",
                table: "students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ComlexLevel2Date",
                schema: "members",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComlexLevel2Score",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComlexLevel3",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComlexLevel3Attempts",
                schema: "members",
                table: "students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ComlexLevel3Date",
                schema: "members",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComlexLevel3Score",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EcfmgCertified",
                schema: "members",
                table: "students",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EducationYear",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "GraduationDate",
                schema: "members",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAmsa",
                schema: "members",
                table: "students",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndbe",
                schema: "members",
                table: "students",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLeadership",
                schema: "members",
                table: "students",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsToefl",
                schema: "members",
                table: "students",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Undergrad",
                schema: "members",
                table: "students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsmleAttempts1",
                schema: "members",
                table: "students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsmleAttempts2",
                schema: "members",
                table: "students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsmleAttempts3",
                schema: "members",
                table: "students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UsmleDate1",
                schema: "members",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UsmleDate2",
                schema: "members",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UsmleDate3",
                schema: "members",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsmleScore1",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsmleScore2",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsmleScore3",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsmleStep1",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsmleStep2",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsmleStep3",
                schema: "members",
                table: "students",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "members",
                table: "students",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-0000-0000-0000-000000000001"),
                columns: new[] { "AppliedMatch", "Association", "ComlexLevel1Passed", "ComlexLevel1Taken", "ComlexLevel2", "ComlexLevel2Attempts", "ComlexLevel2Date", "ComlexLevel2Score", "ComlexLevel3", "ComlexLevel3Attempts", "ComlexLevel3Date", "ComlexLevel3Score", "EcfmgCertified", "EducationYear", "GraduationDate", "IsAmsa", "IsIndbe", "IsLeadership", "IsToefl", "Undergrad", "UsmleAttempts1", "UsmleAttempts2", "UsmleAttempts3", "UsmleDate1", "UsmleDate2", "UsmleDate3", "UsmleScore1", "UsmleScore2", "UsmleScore3", "UsmleStep1", "UsmleStep2", "UsmleStep3" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                schema: "members",
                table: "students",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-0000-0000-0000-000000000002"),
                columns: new[] { "AppliedMatch", "Association", "ComlexLevel1Passed", "ComlexLevel1Taken", "ComlexLevel2", "ComlexLevel2Attempts", "ComlexLevel2Date", "ComlexLevel2Score", "ComlexLevel3", "ComlexLevel3Attempts", "ComlexLevel3Date", "ComlexLevel3Score", "EcfmgCertified", "EducationYear", "GraduationDate", "IsAmsa", "IsIndbe", "IsLeadership", "IsToefl", "Undergrad", "UsmleAttempts1", "UsmleAttempts2", "UsmleAttempts3", "UsmleDate1", "UsmleDate2", "UsmleDate3", "UsmleScore1", "UsmleScore2", "UsmleScore3", "UsmleStep1", "UsmleStep2", "UsmleStep3" },
                values: new object[] { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedMatch",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "Association",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel1Passed",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel1Taken",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel2",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel2Attempts",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel2Date",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel2Score",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel3",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel3Attempts",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel3Date",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "ComlexLevel3Score",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "EcfmgCertified",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "EducationYear",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "GraduationDate",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "IsAmsa",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "IsIndbe",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "IsLeadership",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "IsToefl",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "Undergrad",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleAttempts1",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleAttempts2",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleAttempts3",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleDate1",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleDate2",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleDate3",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleScore1",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleScore2",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleScore3",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleStep1",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleStep2",
                schema: "members",
                table: "students");

            migrationBuilder.DropColumn(
                name: "UsmleStep3",
                schema: "members",
                table: "students");
        }
    }
}
