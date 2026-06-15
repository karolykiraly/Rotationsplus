using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "marketplace");

            migrationBuilder.CreateTable(
                name: "specialties",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialties", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "marketplace",
                table: "specialties",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Internal Medicine" },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "General Surgery" },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Family Medicine" },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Psychiatry" },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Neurology" },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000006"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Pathology" },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000007"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Pediatrics" },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000008"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "OBGYN" },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000009"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Orthopedic Surgery" },
                    { new Guid("aaaaaaaa-0000-0000-0000-00000000000a"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Radiology" },
                    { new Guid("aaaaaaaa-0000-0000-0000-00000000000b"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Anesthesiology" },
                    { new Guid("aaaaaaaa-0000-0000-0000-00000000000c"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Dermatology" },
                    { new Guid("aaaaaaaa-0000-0000-0000-00000000000d"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Emergency Medicine" },
                    { new Guid("aaaaaaaa-0000-0000-0000-00000000000e"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Hematology/Oncology" },
                    { new Guid("aaaaaaaa-0000-0000-0000-00000000000f"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "All Core Rotations" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_specialties_Name",
                schema: "marketplace",
                table: "specialties",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "specialties",
                schema: "marketplace");
        }
    }
}
