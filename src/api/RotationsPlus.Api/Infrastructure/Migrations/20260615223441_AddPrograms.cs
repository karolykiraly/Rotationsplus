using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "programs",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MaxStudentsPerRotation = table.Column<int>(type: "integer", nullable: false),
                    MinWeeksPerRotation = table.Column<int>(type: "integer", nullable: false),
                    RetailAmountPerWeek = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    WeeklyHonorarium = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_programs_specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalSchema: "marketplace",
                        principalTable: "specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "marketplace",
                table: "programs",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Description", "IsDeleted", "MaxStudentsPerRotation", "MinWeeksPerRotation", "ModifiedAtUtc", "ModifiedBy", "ProgramType", "RetailAmountPerWeek", "SpecialtyId", "WeeklyHonorarium" },
                values: new object[,]
                {
                    { new Guid("cccccccc-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "In-person internal medicine rotation.", false, 2, 4, null, null, "InPerson", 1500m, new Guid("aaaaaaaa-0000-0000-0000-000000000001"), 500m },
                    { new Guid("cccccccc-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "Remote internal medicine tele-rotation.", false, 4, 2, null, null, "TeleRotation", 1000m, new Guid("aaaaaaaa-0000-0000-0000-000000000001"), 300m },
                    { new Guid("cccccccc-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "Hands-on pediatrics rotation.", false, 1, 4, null, null, "InPerson", 1800m, new Guid("aaaaaaaa-0000-0000-0000-000000000007"), 600m },
                    { new Guid("cccccccc-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "Family medicine consultation rotation.", false, 3, 2, null, null, "Consultation", 900m, new Guid("aaaaaaaa-0000-0000-0000-000000000003"), 250m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_programs_SpecialtyId",
                schema: "marketplace",
                table: "programs",
                column: "SpecialtyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "programs",
                schema: "marketplace");
        }
    }
}
