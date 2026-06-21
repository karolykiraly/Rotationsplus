using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Marketplace program catalog endpoints. Reads are open to any marketplace viewer (staff +
/// signed-in customers); writes are AdminOnly. The preceptor's honorarium is staff-only on the
/// detail. Money and capacity fields are validated strictly here — pricing is a risk area.
/// </summary>
public static class ProgramEndpoints
{
    public static IEndpointRouteBuilder MapProgramEndpoints(this IEndpointRouteBuilder routes)
    {
        // Reads are open to any marketplace viewer (staff + signed-in customers); writes add AdminOnly.
        var group = routes.MapGroup("/api/programs")
            .RequireAuthorization(AuthorizationPolicies.MarketplaceViewer)
            .WithTags("Marketplace");

        group.MapGet("/", async (
            Guid? specialtyId, Guid? preceptorId, ProgramType? programType, decimal? maxRetailPerWeek, string? q,
            RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            // Catalog search/filter. All filters are optional and AND together; omitting one widens
            // the result. Open to staff + signed-in customers (MarketplaceViewer).
            var query = db.Programs.AsQueryable();

            if (specialtyId is { } sid) query = query.Where(p => p.SpecialtyId == sid);
            if (preceptorId is { } pid) query = query.Where(p => p.PreceptorId == pid);
            if (programType is { } pt) query = query.Where(p => p.ProgramType == pt);
            if (maxRetailPerWeek is { } max) query = query.Where(p => p.RetailAmountPerWeek <= max);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                if (term.Length > MaxSearchLength)
                {
                    return Results.BadRequest($"q must be {MaxSearchLength} characters or fewer.");
                }

                // Escape ILIKE wildcards (\ % _) so user input matches literally; case-insensitive contains.
                var escaped = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                var pattern = $"%{escaped}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Specialty.Name, pattern) ||
                    (p.Description != null && EF.Functions.ILike(p.Description, pattern)));
            }

            var programs = await query
                .OrderBy(p => p.Specialty.Name)
                .ThenBy(p => p.ProgramType)
                .Select(p => new ProgramSummaryResponse(
                    p.Id,
                    p.ProgramNumber,
                    p.Specialty.Name,
                    p.ProgramType,
                    p.MaxStudentsPerRotation,
                    p.MinWeeksPerRotation,
                    p.RetailAmountPerWeek,
                    p.Preceptor != null ? p.Preceptor.FirstName + " " + p.Preceptor.LastName : null,
                    p.City,
                    p.State,
                    p.IsOpen,
                    p.Tags))
                .ToListAsync(cancellationToken);

            return Results.Ok(programs);
        })
        .WithName("ListPrograms");

        group.MapGet("/{id:guid}", async (Guid id, ICurrentUser user, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            // Honorarium (preceptor pay → platform margin) is staff-only; hide it from customers.
            var includeHonorarium = user.Roles.Any(RoleNames.Staff.Contains);

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
                    includeHonorarium ? p.WeeklyHonorarium : null,
                    p.Description,
                    p.PreceptorId,
                    p.Preceptor != null ? p.Preceptor.FirstName + " " + p.Preceptor.LastName : null,
                    p.IsOpen,
                    p.ProgramNumber,
                    p.City,
                    p.State,
                    p.Tags))
                .FirstOrDefaultAsync(cancellationToken);

            return program is null ? Results.NotFound() : Results.Ok(program);
        })
        .WithName("GetProgram");

        // ---- Admin writes (AdminOnly stacks on the group's MarketplaceViewer) ----

        group.MapPost("/", async (CreateProgramRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryValidate(request.ProgramType, request.MaxStudentsPerRotation, request.MinWeeksPerRotation,
                    request.RetailAmountPerWeek, request.WeeklyHonorarium, request.Description, request.City, request.State, request.Tags,
                    out var description, out var city, out var state, out var tags, out var error))
            {
                return Results.BadRequest(error);
            }

            var specialtyName = await ResolveSpecialtyNameAsync(db, request.SpecialtyId, cancellationToken);
            if (specialtyName is null)
            {
                return Results.BadRequest($"Specialty '{request.SpecialtyId}' does not exist.");
            }

            var (preceptorOk, preceptorName, preceptorError) = await ResolvePreceptorAsync(db, request.PreceptorId, cancellationToken);
            if (!preceptorOk)
            {
                return Results.BadRequest(preceptorError);
            }

            var program = new RotationProgram
            {
                SpecialtyId = request.SpecialtyId,
                PreceptorId = request.PreceptorId,
                ProgramType = request.ProgramType,
                MaxStudentsPerRotation = request.MaxStudentsPerRotation,
                MinWeeksPerRotation = request.MinWeeksPerRotation,
                RetailAmountPerWeek = request.RetailAmountPerWeek,
                WeeklyHonorarium = request.WeeklyHonorarium,
                IsOpen = request.IsOpen,
                City = city,
                State = state,
                Tags = tags,
                Description = description,
            };
            db.Programs.Add(program);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/programs/{program.Id}", ToDetail(program, specialtyName, preceptorName));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("CreateProgram");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProgramRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryValidate(request.ProgramType, request.MaxStudentsPerRotation, request.MinWeeksPerRotation,
                    request.RetailAmountPerWeek, request.WeeklyHonorarium, request.Description, request.City, request.State, request.Tags,
                    out var description, out var city, out var state, out var tags, out var error))
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

            var (preceptorOk, preceptorName, preceptorError) = await ResolvePreceptorAsync(db, request.PreceptorId, cancellationToken);
            if (!preceptorOk)
            {
                return Results.BadRequest(preceptorError);
            }

            program.SpecialtyId = request.SpecialtyId;
            program.PreceptorId = request.PreceptorId;
            program.ProgramType = request.ProgramType;
            program.MaxStudentsPerRotation = request.MaxStudentsPerRotation;
            program.MinWeeksPerRotation = request.MinWeeksPerRotation;
            program.RetailAmountPerWeek = request.RetailAmountPerWeek;
            program.WeeklyHonorarium = request.WeeklyHonorarium;
            program.IsOpen = request.IsOpen;
            program.City = city;
            program.State = state;
            program.Tags = tags;
            program.Description = description;
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToDetail(program, specialtyName, preceptorName));
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

            // Block deletion while live rotations reference this program. The FK is Restrict, but that
            // only governs hard deletes — a soft-delete is an UPDATE, so without this guard the program
            // would be filtered out from under its rotations and the admin rotations list (which projects
            // the program's non-nullable ProgramType/SpecialtyName through the navigation) would 500.
            // Soft-deleted rotations don't count: the global filter already hides them from that list.
            if (await db.Rotations.AnyAsync(r => r.ProgramId == id, cancellationToken))
            {
                return Results.Conflict("This program has rotations booked against it and can't be deleted.");
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
    private const int MaxSearchLength = 100; // bound the free-text search term (cheap DoS guard).

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

    private const int MaxLocationLength = 120;
    private const int MaxTags = 20;
    private const int MaxTagLength = 60;

    private static bool TryValidate(
        ProgramType programType, int maxStudents, int minWeeks, decimal retailPerWeek, decimal weeklyHonorarium,
        string? description, string? city, string? state, IReadOnlyList<string>? tags,
        out string? normalizedDescription, out string? normalizedCity, out string? normalizedState,
        out List<string> normalizedTags, out string error)
    {
        normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        normalizedCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        normalizedState = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
        normalizedTags = [];
        error = string.Empty;

        if (!Enum.IsDefined(programType))
        {
            error = "ProgramType is invalid.";
            return false;
        }

        if (normalizedCity is { Length: > MaxLocationLength })
        {
            error = $"City must be {MaxLocationLength} characters or fewer.";
            return false;
        }

        if (normalizedState is { Length: > MaxLocationLength })
        {
            error = $"State must be {MaxLocationLength} characters or fewer.";
            return false;
        }

        if (tags is not null)
        {
            // Trim, drop blanks, de-dupe case-insensitively (preserving first-seen casing), bound size.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in tags)
            {
                var tag = raw?.Trim();
                if (string.IsNullOrEmpty(tag)) continue;
                if (tag.Length > MaxTagLength)
                {
                    error = $"Each tag must be {MaxTagLength} characters or fewer.";
                    return false;
                }
                if (seen.Add(tag)) normalizedTags.Add(tag);
            }

            if (normalizedTags.Count > MaxTags)
            {
                error = $"A program can have at most {MaxTags} tags.";
                return false;
            }
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

    /// <summary>Resolves the optional preceptor: (true, null) when none requested; (true, name) when
    /// it exists; (false, error) when a non-existent (or soft-deleted) preceptor id was supplied.</summary>
    private static async Task<(bool Ok, string? Name, string? Error)> ResolvePreceptorAsync(
        RotationsDbContext db, Guid? preceptorId, CancellationToken cancellationToken)
    {
        if (preceptorId is not Guid id)
        {
            return (true, null, null);
        }

        var name = await db.Preceptors
            .Where(p => p.Id == id)
            .Select(p => (string?)(p.FirstName + " " + p.LastName))
            .FirstOrDefaultAsync(cancellationToken);

        return name is null ? (false, null, $"Preceptor '{id}' does not exist.") : (true, name, null);
    }

    private static ProgramDetailResponse ToDetail(RotationProgram p, string specialtyName, string? preceptorName) =>
        new(p.Id, p.SpecialtyId, specialtyName, p.ProgramType, p.MaxStudentsPerRotation,
            p.MinWeeksPerRotation, p.RetailAmountPerWeek, p.WeeklyHonorarium, p.Description, p.PreceptorId, preceptorName,
            p.IsOpen, p.ProgramNumber, p.City, p.State, p.Tags);
}
