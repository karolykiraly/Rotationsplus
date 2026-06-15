using Microsoft.EntityFrameworkCore;
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

        return routes;
    }
}
