using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>The ANONYMOUS public marketing feed. Powers the landing hero's filter dropdowns
/// (specialties, cities, states, durations) for visitors who aren't signed in. It returns only open
/// programs with public-safe fields (no honorarium, preceptor identity, or description) — the real
/// search and program detail still require a customer sign-in. Kept as its own endpoint (not the
/// MarketplaceViewer-gated /api/programs group) so it can be <c>AllowAnonymous</c> without loosening
/// the authenticated catalog.</summary>
public static class PublicCatalogEndpoints
{
    public static IEndpointRouteBuilder MapPublicCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/public")
            .AllowAnonymous()
            .WithTags("Public");

        // Open programs that populate the public landing hero's filter dropdowns. No paging — the public
        // marketing feed is small and a generous server cap bounds it.
        group.MapGet("/programs", async (
            IProgramImageStore imageStore, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            const int MaxPublicPrograms = 500;

            var programs = await db.Programs
                .Where(p => p.IsOpen)
                .OrderBy(p => p.Specialty.Name)
                .ThenBy(p => p.ProgramNumber)
                .Take(MaxPublicPrograms)
                .Select(p => new PublicProgramResponse(
                    p.Id,
                    p.ProgramNumber,
                    p.Specialty.Name,
                    p.ProgramType,
                    p.City,
                    p.State,
                    p.RetailAmountPerWeek,
                    p.MinWeeksPerRotation,
                    p.IsOpen,
                    p.ImageBlobName)) // raw blob name in SQL; signed to a read URL after materialization
                .ToListAsync(cancellationToken);

            var withUrls = programs
                .Select(p => p with { ImageUrl = imageStore.GetReadUrl(p.ImageUrl) })
                .ToList();

            return Results.Ok(withUrls);
        })
        .WithName("ListPublicPrograms");

        return routes;
    }
}
