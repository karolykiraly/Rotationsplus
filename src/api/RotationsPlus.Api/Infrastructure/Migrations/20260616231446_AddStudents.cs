using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "members");

            migrationBuilder.CreateTable(
                name: "students",
                schema: "members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MobilePhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AcademicStatus = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    VisaStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MedicalSchool = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MedicalSchoolCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StudentOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_students", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "members",
                table: "students",
                columns: new[] { "Id", "AcademicStatus", "City", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Email", "FirstName", "IsDeleted", "LastName", "MedicalSchool", "MedicalSchoolCountry", "MobilePhone", "ModifiedAtUtc", "ModifiedBy", "State", "Status", "StudentOid", "VisaStatus" },
                values: new object[,]
                {
                    { new Guid("ffffffff-0000-0000-0000-000000000001"), "InternationalMedicalGraduate", "Chicago", new DateTimeOffset(new DateTime(2026, 6, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "sam.rivera@example.com", "Sam", false, "Rivera", null, null, null, null, null, "IL", "MemberActivated", null, "NeedsVisaHelp" },
                    { new Guid("ffffffff-0000-0000-0000-000000000002"), "MdStudent", "Houston", new DateTimeOffset(new DateTime(2026, 6, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, "dana.cole@example.com", "Dana", false, "Cole", null, null, null, null, null, "TX", "Registered", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_students_Email",
                schema: "members",
                table: "students",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_students_Status",
                schema: "members",
                table: "students",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "students",
                schema: "members");
        }
    }
}
