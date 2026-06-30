using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProgramName",
                schema: "marketplace",
                table: "programs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000001"),
                column: "ProgramName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000002"),
                column: "ProgramName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000003"),
                column: "ProgramName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000004"),
                column: "ProgramName",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgramName",
                schema: "marketplace",
                table: "programs");
        }
    }
}
