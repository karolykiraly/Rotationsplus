using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreceptorApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "marketplace",
                table: "preceptors",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAtUtc",
                schema: "marketplace",
                table: "preceptors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                schema: "marketplace",
                table: "preceptors",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "preceptors",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-0000-0000-0000-000000000001"),
                columns: new[] { "RejectionReason", "ReviewedAtUtc", "ReviewedBy" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "preceptors",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-0000-0000-0000-000000000002"),
                columns: new[] { "RejectionReason", "ReviewedAtUtc", "ReviewedBy" },
                values: new object[] { null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "marketplace",
                table: "preceptors");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                schema: "marketplace",
                table: "preceptors");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                schema: "marketplace",
                table: "preceptors");
        }
    }
}
