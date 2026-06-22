using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRotationDateIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rotations_Status",
                schema: "operations",
                table: "rotations");

            migrationBuilder.CreateIndex(
                name: "IX_rotations_StartDate",
                schema: "operations",
                table: "rotations",
                column: "StartDate",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_rotations_Status_StartDate",
                schema: "operations",
                table: "rotations",
                columns: new[] { "Status", "StartDate" },
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rotations_StartDate",
                schema: "operations",
                table: "rotations");

            migrationBuilder.DropIndex(
                name: "IX_rotations_Status_StartDate",
                schema: "operations",
                table: "rotations");

            migrationBuilder.CreateIndex(
                name: "IX_rotations_Status",
                schema: "operations",
                table: "rotations",
                column: "Status");
        }
    }
}
