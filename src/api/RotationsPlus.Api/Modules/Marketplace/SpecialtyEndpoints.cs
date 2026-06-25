using Microsoft.EntityFrameworkCore;
using Npgsql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Marketplace specialty reference-data endpoints. Reads are open to any marketplace viewer (staff +
/// signed-in customers); writes are AdminOnly.
/// </summary>
public static class SpecialtyEndpoints
{
    public static IEndpointRouteBuilder MapSpecialtyEndpoints(this IEndpointRouteBuilder routes)
    {
        // Reads are open to any marketplace viewer (staff + signed-in customers); writes add AdminOnly.
        var group = routes.MapGroup("/api/specialties")
            .RequireAuthorization(AuthorizationPolicies.MarketplaceViewer)
            .WithTags("Marketplace");

        group.MapGet("/", async (RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var specialties = await db.Specialties
                .OrderBy(s => s.Name)
                .Select(s => new SpecialtyResponse(s.Id, s.Name))
                .ToListAsync(cancellationToken);

            return Results.Ok(specialties);
        })
        .WithName("ListSpecialties");

        group.MapGet("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var specialty = await db.Specialties
                .Where(s => s.Id == id)
                .Select(s => new SpecialtyResponse(s.Id, s.Name))
                .FirstOrDefaultAsync(cancellationToken);

            return specialty is null ? Results.NotFound() : Results.Ok(specialty);
        })
        .WithName("GetSpecialty");

        // ---- Admin writes (AdminOnly stacks on the group's MarketplaceViewer) ----

        group.MapPost("/", async (CreateSpecialtyRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryNormalizeName(request.Name, out var name, out var error))
            {
                return Results.BadRequest(error);
            }

            // Match past the soft-delete filter so the unique index can't be violated: an existing
            // active name conflicts; a soft-deleted one is restored instead of inserting a duplicate.
            var existing = await db.Specialties
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Name == name, cancellationToken);

            if (existing is { IsDeleted: false })
            {
                return Results.Conflict($"A specialty named '{name}' already exists.");
            }

            if (existing is { IsDeleted: true })
            {
                existing.IsDeleted = false;
                existing.DeletedAtUtc = null;
                existing.DeletedBy = null;
                await db.SaveChangesAsync(cancellationToken);
                return Results.Created($"/api/specialties/{existing.Id}", new SpecialtyResponse(existing.Id, existing.Name));
            }

            var specialty = new Specialty { Name = name };
            db.Specialties.Add(specialty);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
            {
                // Lost a concurrent create race for the same name; the unique index rejected the insert.
                return Results.Conflict($"A specialty named '{name}' already exists.");
            }

            return Results.Created($"/api/specialties/{specialty.Id}", new SpecialtyResponse(specialty.Id, specialty.Name));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("CreateSpecialty");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSpecialtyRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryNormalizeName(request.Name, out var name, out var error))
            {
                return Results.BadRequest(error);
            }

            var specialty = await db.Specialties.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (specialty is null)
            {
                return Results.NotFound();
            }

            var nameTaken = await db.Specialties
                .IgnoreQueryFilters()
                .AnyAsync(s => s.Name == name && s.Id != id, cancellationToken);
            if (nameTaken)
            {
                return Results.Conflict($"A specialty named '{name}' already exists.");
            }

            specialty.Name = name;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new SpecialtyResponse(specialty.Id, specialty.Name));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateSpecialty");

        group.MapDelete("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var specialty = await db.Specialties.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (specialty is null)
            {
                return Results.NotFound();
            }

            // Block deletion while any live row references this specialty. A soft-delete is an UPDATE
            // (the FK Restrict only governs hard deletes), so without this guard the specialty would be
            // filtered out from under its references — and anything projecting the required Specialty
            // navigation (the rotations list + dashboard via Program→Specialty; the preceptor list/detail
            // via Preceptor→PrimarySpecialty) would read a NULL name. Two FKs point at Specialty:
            // RotationProgram.SpecialtyId and Preceptor.PrimarySpecialtyId — both must be clear. Mirrors
            // the program- and student-delete guards. Soft-deleted referrers don't count (global filter).
            if (await db.Programs.AnyAsync(p => p.SpecialtyId == id, cancellationToken)
                || await db.Preceptors.AnyAsync(p => p.PrimarySpecialtyId == id, cancellationToken))
            {
                return Results.Conflict("This specialty is in use by programs or preceptors and can't be deleted.");
            }

            db.Specialties.Remove(specialty); // interceptor converts the delete into a soft-delete
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DeleteSpecialty");

        return routes;
    }

    private const int MaxNameLength = 200;

    private static bool TryNormalizeName(string? input, out string name, out string error)
    {
        name = input?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            error = "Name is required.";
            return false;
        }

        if (name.Length > MaxNameLength)
        {
            error = $"Name must be {MaxNameLength} characters or fewer.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
