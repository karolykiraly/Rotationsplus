using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreceptors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "preceptors",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PrimarySpecialtyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalLicenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LicenseState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Bio = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_preceptors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_preceptors_specialties_PrimarySpecialtyId",
                        column: x => x.PrimarySpecialtyId,
                        principalSchema: "marketplace",
                        principalTable: "specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "marketplace",
                table: "preceptors",
                columns: new[] { "Id", "Bio", "City", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Email", "FirstName", "IsDeleted", "LastName", "LicenseState", "MedicalLicenseNumber", "ModifiedAtUtc", "ModifiedBy", "PrimarySpecialtyId", "State", "Status" },
                values: new object[,]
                {
                    { new Guid("dddddddd-0000-0000-0000-000000000001"), null, "Chicago", new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "jane.carter@example.com", "Jane", false, "Carter", "IL", null, null, null, new Guid("aaaaaaaa-0000-0000-0000-000000000001"), "IL", "MemberActivated" },
                    { new Guid("dddddddd-0000-0000-0000-000000000002"), null, "Houston", new DateTimeOffset(new DateTime(2026, 6, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "omar.reyes@example.com", "Omar", false, "Reyes", "TX", null, null, null, new Guid("aaaaaaaa-0000-0000-0000-000000000007"), "TX", "MemberValidated" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_preceptors_Email",
                schema: "marketplace",
                table: "preceptors",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_preceptors_PrimarySpecialtyId",
                schema: "marketplace",
                table: "preceptors",
                column: "PrimarySpecialtyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "preceptors",
                schema: "marketplace");
        }
    }
}
