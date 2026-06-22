using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramDocumentDueDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentDueDays",
                schema: "marketplace",
                table: "programs",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000001"),
                column: "DocumentDueDays",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000002"),
                column: "DocumentDueDays",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000003"),
                column: "DocumentDueDays",
                value: 14);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000004"),
                column: "DocumentDueDays",
                value: 14);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentDueDays",
                schema: "marketplace",
                table: "programs");
        }
    }
}
