using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Marketplace program catalog endpoints (read-only for now; StaffOnly until CIAM lands).
/// Admin writes and the preceptor association arrive in later slices.
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

        return routes;
    }
}
