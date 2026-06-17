using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRotationStudentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                schema: "operations",
                table: "rotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "operations",
                table: "rotations",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-0000-0000-0000-000000000001"),
                column: "StudentId",
                value: new Guid("ffffffff-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_rotations_StudentId",
                schema: "operations",
                table: "rotations",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_rotations_students_StudentId",
                schema: "operations",
                table: "rotations",
                column: "StudentId",
                principalSchema: "members",
                principalTable: "students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rotations_students_StudentId",
                schema: "operations",
                table: "rotations");

            migrationBuilder.DropIndex(
                name: "IX_rotations_StudentId",
                schema: "operations",
                table: "rotations");

            migrationBuilder.DropColumn(
                name: "StudentId",
                schema: "operations",
                table: "rotations");
        }
    }
}
