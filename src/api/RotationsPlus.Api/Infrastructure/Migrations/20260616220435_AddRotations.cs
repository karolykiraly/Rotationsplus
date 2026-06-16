using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "rotations",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StudentEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StudentOid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Weeks = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_rotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rotations_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "marketplace",
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "operations",
                table: "rotations",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "EndDate", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "ProgramId", "StartDate", "Status", "StudentEmail", "StudentName", "StudentOid", "Weeks" },
                values: new object[] { new Guid("eeeeeeee-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new DateOnly(2026, 8, 3), false, null, null, new Guid("cccccccc-0000-0000-0000-000000000001"), new DateOnly(2026, 7, 6), "Active", "sam.rivera@example.com", "Sam Rivera", null, 4 });

            migrationBuilder.CreateIndex(
                name: "IX_rotations_ProgramId",
                schema: "operations",
                table: "rotations",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_rotations_Status",
                schema: "operations",
                table: "rotations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rotations",
                schema: "operations");
        }
    }
}
