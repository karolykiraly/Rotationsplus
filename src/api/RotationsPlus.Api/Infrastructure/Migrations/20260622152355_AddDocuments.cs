using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.CreateTable(
                name: "document_types",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_document_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "program_required_documents",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_program_required_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_required_documents_document_types_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "documents",
                        principalTable: "document_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_required_documents_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "marketplace",
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rotation_documents",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FileBlobName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_rotation_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rotation_documents_document_types_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "documents",
                        principalTable: "document_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rotation_documents_rotations_RotationId",
                        column: x => x.RotationId,
                        principalSchema: "operations",
                        principalTable: "rotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rotation_documents_students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "members",
                        principalTable: "students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "documents",
                table: "document_types",
                columns: new[] { "Id", "Category", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { new Guid("d0c00000-0000-0000-0000-000000000001"), "Immunization", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "COVID-19 Vaccine" },
                    { new Guid("d0c00000-0000-0000-0000-000000000002"), "Immunization", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Hepatitis B" },
                    { new Guid("d0c00000-0000-0000-0000-000000000003"), "Immunization", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "MMR (Measles/Mumps/Rubella)" },
                    { new Guid("d0c00000-0000-0000-0000-000000000004"), "Immunization", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Varicella" },
                    { new Guid("d0c00000-0000-0000-0000-000000000005"), "Immunization", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Tdap" },
                    { new Guid("d0c00000-0000-0000-0000-000000000006"), "MedicalTest", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "TB Test (PPD/Quantiferon)" },
                    { new Guid("d0c00000-0000-0000-0000-000000000007"), "MedicalTest", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "10-Panel Drug Screen" },
                    { new Guid("d0c00000-0000-0000-0000-000000000008"), "Identity", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Proof of Identity" },
                    { new Guid("d0c00000-0000-0000-0000-000000000009"), "Insurance", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Proof of Health Insurance" },
                    { new Guid("d0c00000-0000-0000-0000-00000000000a"), "Insurance", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Proof of Liability Insurance" },
                    { new Guid("d0c00000-0000-0000-0000-00000000000b"), "Certification", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Basic Life Support (BLS)" },
                    { new Guid("d0c00000-0000-0000-0000-00000000000c"), "Certification", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "HIPAA Training" },
                    { new Guid("d0c00000-0000-0000-0000-00000000000d"), "Professional", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Curriculum Vitae (CV)" },
                    { new Guid("d0c00000-0000-0000-0000-00000000000e"), "Professional", new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, false, null, null, "Photo for ID Badge" }
                });

            migrationBuilder.InsertData(
                schema: "documents",
                table: "program_required_documents",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "DocumentTypeId", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "ProgramId" },
                values: new object[,]
                {
                    { new Guid("d12e0000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new Guid("d0c00000-0000-0000-0000-000000000001"), false, null, null, new Guid("cccccccc-0000-0000-0000-000000000001") },
                    { new Guid("d12e0000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new Guid("d0c00000-0000-0000-0000-000000000008"), false, null, null, new Guid("cccccccc-0000-0000-0000-000000000001") },
                    { new Guid("d12e0000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new Guid("d0c00000-0000-0000-0000-00000000000b"), false, null, null, new Guid("cccccccc-0000-0000-0000-000000000001") },
                    { new Guid("d12e0000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new Guid("d0c00000-0000-0000-0000-00000000000d"), false, null, null, new Guid("cccccccc-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                schema: "documents",
                table: "rotation_documents",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "DocumentTypeId", "DueDate", "FileBlobName", "FileName", "IsDeleted", "ModifiedAtUtc", "ModifiedBy", "RejectionReason", "ReviewedAtUtc", "ReviewedBy", "RotationId", "Status", "StudentId", "SubmittedAtUtc" },
                values: new object[,]
                {
                    { new Guid("d40c0000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new Guid("d0c00000-0000-0000-0000-000000000001"), new DateOnly(2026, 6, 22), null, "covid-card.pdf", false, null, null, null, null, null, new Guid("eeeeeeee-0000-0000-0000-000000000001"), "Approved", new Guid("ffffffff-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("d40c0000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new Guid("d0c00000-0000-0000-0000-000000000008"), new DateOnly(2026, 6, 22), null, "passport.pdf", false, null, null, null, null, null, new Guid("eeeeeeee-0000-0000-0000-000000000001"), "Submitted", new Guid("ffffffff-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("d40c0000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new Guid("d0c00000-0000-0000-0000-00000000000b"), new DateOnly(2026, 6, 22), null, null, false, null, null, null, null, null, new Guid("eeeeeeee-0000-0000-0000-000000000001"), "UploadNeeded", new Guid("ffffffff-0000-0000-0000-000000000001"), null },
                    { new Guid("d40c0000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", null, null, new Guid("d0c00000-0000-0000-0000-00000000000d"), new DateOnly(2026, 6, 22), null, null, false, null, null, null, null, null, new Guid("eeeeeeee-0000-0000-0000-000000000001"), "UploadNeeded", new Guid("ffffffff-0000-0000-0000-000000000001"), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_types_Name",
                schema: "documents",
                table: "document_types",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_required_documents_DocumentTypeId",
                schema: "documents",
                table: "program_required_documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_program_required_documents_ProgramId_DocumentTypeId",
                schema: "documents",
                table: "program_required_documents",
                columns: new[] { "ProgramId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_documents_DocumentTypeId",
                schema: "documents",
                table: "rotation_documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_documents_RotationId_DocumentTypeId",
                schema: "documents",
                table: "rotation_documents",
                columns: new[] { "RotationId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_documents_StudentId",
                schema: "documents",
                table: "rotation_documents",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_required_documents",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "rotation_documents",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_types",
                schema: "documents");
        }
    }
}
