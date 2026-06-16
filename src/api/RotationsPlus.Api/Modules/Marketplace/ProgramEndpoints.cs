using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Marketplace program catalog endpoints. Reads are StaffOnly (until CIAM lands); writes are
/// AdminOnly. The preceptor association arrives in a later slice. Money and capacity fields are
/// validated strictly here — pricing is a risk area.
/// </summary>
public static class ProgramEndpoints
{
    public static IEndpointRouteBuilder MapProgramEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/programs")
            .RequireAuthorization(AuthorizationPolicies.StaffOnly)
            .WithTags("Marketplace");

        group.MapGet("/", async (RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var programs = await db.Programs
                .OrderBy(p => p.Specialty.Name)
                .ThenBy(p => p.ProgramType)
                .Select(p => new ProgramSummaryResponse(
                    p.Id,
                    p.Specialty.Name,
                    p.ProgramType,
                    p.MaxStudentsPerRotation,
                    p.MinWeeksPerRotation,
                    p.RetailAmountPerWeek))
                .ToListAsync(cancellationToken);

            return Results.Ok(programs);
        })
        .WithName("ListPrograms");

        group.MapGet("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var program = await db.Programs
                .Where(p => p.Id == id)
                .Select(p => new ProgramDetailResponse(
                    p.Id,
                    p.SpecialtyId,
                    p.Specialty.Name,
                    p.ProgramType,
                    p.MaxStudentsPerRotation,
                    p.MinWeeksPerRotation,
                    p.RetailAmountPerWeek,
                    p.WeeklyHonorarium,
                    p.Description))
                .FirstOrDefaultAsync(cancellationToken);

            return program is null ? Results.NotFound() : Results.Ok(program);
        })
        .WithName("GetProgram");

        // ---- Admin writes (AdminOnly stacks on the group's StaffOnly) ----

        group.MapPost("/", async (CreateProgramRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryValidate(request.ProgramType, request.MaxStudentsPerRotation, request.MinWeeksPerRotation,
                    request.RetailAmountPerWeek, request.WeeklyHonorarium, request.Description, out var description, out var error))
            {
                return Results.BadRequest(error);
            }

            var specialtyName = await ResolveSpecialtyNameAsync(db, request.SpecialtyId, cancellationToken);
            if (specialtyName is null)
            {
                return Results.BadRequest($"Specialty '{request.SpecialtyId}' does not exist.");
            }

            var program = new RotationProgram
            {
                SpecialtyId = request.SpecialtyId,
                ProgramType = request.ProgramType,
                MaxStudentsPerRotation = request.MaxStudentsPerRotation,
                MinWeeksPerRotation = request.MinWeeksPerRotation,
                RetailAmountPerWeek = request.RetailAmountPerWeek,
                WeeklyHonorarium = request.WeeklyHonorarium,
                Description = description,
            };
            db.Programs.Add(program);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/programs/{program.Id}", ToDetail(program, specialtyName));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("CreateProgram");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProgramRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryValidate(request.ProgramType, request.MaxStudentsPerRotation, request.MinWeeksPerRotation,
                    request.RetailAmountPerWeek, request.WeeklyHonorarium, request.Description, out var description, out var error))
            {
                return Results.BadRequest(error);
            }

            var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (program is null)
            {
                return Results.NotFound();
            }

            var specialtyName = await ResolveSpecialtyNameAsync(db, request.SpecialtyId, cancellationToken);
            if (specialtyName is null)
            {
                return Results.BadRequest($"Specialty '{request.SpecialtyId}' does not exist.");
            }

            program.SpecialtyId = request.SpecialtyId;
            program.ProgramType = request.ProgramType;
            program.MaxStudentsPerRotation = request.MaxStudentsPerRotation;
            program.MinWeeksPerRotation = request.MinWeeksPerRotation;
            program.RetailAmountPerWeek = request.RetailAmountPerWeek;
            program.WeeklyHonorarium = request.WeeklyHonorarium;
            program.Description = description;
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToDetail(program, specialtyName));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateProgram");

        group.MapDelete("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (program is null)
            {
                return Results.NotFound();
            }

            db.Programs.Remove(program); // interceptor converts the delete into a soft-delete
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DeleteProgram");

        return routes;
    }

    private const int MaxDescriptionLength = 4000;

    // Capacity sanity caps (the columns are int; these guard against absurd input).
    private const int MaxStudentsCap = 1000;
    private const int MaxWeeksCap = 520; // ~10 years — generous upper bound.

    // Money ceiling = the numeric(10,2) column maximum. Values above this would overflow the
    // column (Postgres error -> 500); reject them cleanly here instead.
    private const decimal MaxMoney = 99_999_999.99m;

    /// <summary>Name of the referenced specialty, or null if it doesn't exist (the global query
    /// filter already excludes soft-deleted specialties, so a deleted one reads as missing).</summary>
    private static Task<string?> ResolveSpecialtyNameAsync(RotationsDbContext db, Guid specialtyId, CancellationToken cancellationToken) =>
        db.Specialties
            .Where(s => s.Id == specialtyId)
            .Select(s => (string?)s.Name)
            .FirstOrDefaultAsync(cancellationToken);

    private static bool TryValidate(
        ProgramType programType, int maxStudents, int minWeeks, decimal retailPerWeek, decimal weeklyHonorarium,
        string? description, out string? normalizedDescription, out string error)
    {
        normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        error = string.Empty;

        if (!Enum.IsDefined(programType))
        {
            error = "ProgramType is invalid.";
            return false;
        }

        if (maxStudents is < 1 or > MaxStudentsCap)
        {
            error = $"MaxStudentsPerRotation must be between 1 and {MaxStudentsCap}.";
            return false;
        }

        if (minWeeks is < 1 or > MaxWeeksCap)
        {
            error = $"MinWeeksPerRotation must be between 1 and {MaxWeeksCap}.";
            return false;
        }

        if (!IsValidMoney(retailPerWeek))
        {
            error = "RetailAmountPerWeek must be between 0 and 99,999,999.99 with at most 2 decimal places.";
            return false;
        }

        if (!IsValidMoney(weeklyHonorarium))
        {
            error = "WeeklyHonorarium must be between 0 and 99,999,999.99 with at most 2 decimal places.";
            return false;
        }

        if (normalizedDescription is { Length: > MaxDescriptionLength })
        {
            error = $"Description must be {MaxDescriptionLength} characters or fewer.";
            return false;
        }

        return true;
    }

    /// <summary>Non-negative, within the numeric(10,2) ceiling, and no finer than cents — so the
    /// persisted value (and the create response, which echoes the in-memory entity) is exact.</summary>
    private static bool IsValidMoney(decimal value) =>
        value >= 0 && value <= MaxMoney && decimal.Round(value, 2) == value;

    private static ProgramDetailResponse ToDetail(RotationProgram p, string specialtyName) =>
        new(p.Id, p.SpecialtyId, specialtyName, p.ProgramType, p.MaxStudentsPerRotation,
            p.MinWeeksPerRotation, p.RetailAmountPerWeek, p.WeeklyHonorarium, p.Description);
}
