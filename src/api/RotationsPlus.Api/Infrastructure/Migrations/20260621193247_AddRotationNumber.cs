using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRotationNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RotationNumber",
                schema: "operations",
                table: "rotations",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.UpdateData(
                schema: "operations",
                table: "rotations",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-0000-0000-0000-000000000001"),
                column: "RotationNumber",
                value: 1001);

            migrationBuilder.CreateIndex(
                name: "IX_rotations_RotationNumber",
                schema: "operations",
                table: "rotations",
                column: "RotationNumber",
                unique: true);

            // The identity column back-fills existing rows from the sequence (low values) and the seeded
            // row is set explicitly to 1001. Restart the identity sequence well above both so server-assigned
            // numbers for new rotations never collide. (Quoted column identifier — NOT pg_get_serial_sequence,
            // which down-cases its column arg and would miss "RotationNumber".) At cutover the DataMigrator
            // carries legacy rotation ids into RotationNumber and bumps the sequence past that max.
            migrationBuilder.Sql("ALTER TABLE operations.rotations ALTER COLUMN \"RotationNumber\" RESTART WITH 2000;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rotations_RotationNumber",
                schema: "operations",
                table: "rotations");

            migrationBuilder.DropColumn(
                name: "RotationNumber",
                schema: "operations",
                table: "rotations");
        }
    }
}
