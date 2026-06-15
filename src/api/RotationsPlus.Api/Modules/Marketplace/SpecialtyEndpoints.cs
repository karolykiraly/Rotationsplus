using Microsoft.EntityFrameworkCore;
using Npgsql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Marketplace specialty reference-data endpoints (read-only for now). Staff-only until the CIAM
/// customer directory lands; then browse will be opened to students/preceptors. Admin write
/// endpoints come in a later slice.
/// </summary>
public static class SpecialtyEndpoints
{
    public static IEndpointRouteBuilder MapSpecialtyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/specialties")
            .RequireAuthorization(AuthorizationPolicies.StaffOnly)
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

        // ---- Admin writes (AdminOnly stacks on the group's StaffOnly) ----

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
