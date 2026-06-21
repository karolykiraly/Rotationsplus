using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramImageBlobName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageBlobName",
                schema: "marketplace",
                table: "programs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000001"),
                column: "ImageBlobName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000002"),
                column: "ImageBlobName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000003"),
                column: "ImageBlobName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000004"),
                column: "ImageBlobName",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageBlobName",
                schema: "marketplace",
                table: "programs");
        }
    }
}
