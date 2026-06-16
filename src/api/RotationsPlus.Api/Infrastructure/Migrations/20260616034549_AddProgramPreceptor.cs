using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramPreceptor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreceptorId",
                schema: "marketplace",
                table: "programs",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000001"),
                column: "PreceptorId",
                value: new Guid("dddddddd-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000002"),
                column: "PreceptorId",
                value: new Guid("dddddddd-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000003"),
                column: "PreceptorId",
                value: new Guid("dddddddd-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                schema: "marketplace",
                table: "programs",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000004"),
                column: "PreceptorId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_programs_PreceptorId",
                schema: "marketplace",
                table: "programs",
                column: "PreceptorId");

            migrationBuilder.AddForeignKey(
                name: "FK_programs_preceptors_PreceptorId",
                schema: "marketplace",
                table: "programs",
                column: "PreceptorId",
                principalSchema: "marketplace",
                principalTable: "preceptors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_programs_preceptors_PreceptorId",
                schema: "marketplace",
                table: "programs");

            migrationBuilder.DropIndex(
                name: "IX_programs_PreceptorId",
                schema: "marketplace",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "PreceptorId",
                schema: "marketplace",
                table: "programs");
        }
    }
}
