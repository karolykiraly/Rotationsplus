using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Marketplace preceptor directory endpoints. Reads are StaffOnly (until CIAM lands); writes are
/// AdminOnly. This first slice is the identity/professional core + primary specialty; onboarding,
/// program associations, and the approval flow arrive in later slices (see Plan_Preceptor.md).
/// </summary>
public static class PreceptorEndpoints
{
    public static IEndpointRouteBuilder MapPreceptorEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/preceptors")
            .RequireAuthorization(AuthorizationPolicies.StaffOnly)
            .WithTags("Marketplace");

        group.MapGet("/", async (RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var preceptors = await db.Preceptors
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .Select(p => new PreceptorSummaryResponse(
                    p.Id,
                    p.FirstName + " " + p.LastName,
                    p.Email,
                    p.PrimarySpecialty.Name,
                    p.City,
                    p.State,
                    p.Status))
                .ToListAsync(cancellationToken);

            return Results.Ok(preceptors);
        })
        .WithName("ListPreceptors");

        group.MapGet("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var preceptor = await db.Preceptors
                .Where(p => p.Id == id)
                .Select(p => new PreceptorDetailResponse(
                    p.Id,
                    p.FirstName,
                    p.LastName,
                    p.Email,
                    p.PrimarySpecialtyId,
                    p.PrimarySpecialty.Name,
                    p.MedicalLicenseNumber,
                    p.LicenseState,
                    p.City,
                    p.State,
                    p.Status,
                    p.Bio))
                .FirstOrDefaultAsync(cancellationToken);

            return preceptor is null ? Results.NotFound() : Results.Ok(preceptor);
        })
        .WithName("GetPreceptor");

        // ---- Admin writes (AdminOnly stacks on the group's StaffOnly) ----

        group.MapPost("/", async (CreatePreceptorRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryNormalize(request.FirstName, request.LastName, request.Email, request.PrimarySpecialtyId,
                    request.MedicalLicenseNumber, request.LicenseState, request.City, request.State,
                    request.Status, request.Bio, out var norm, out var error))
            {
                return Results.BadRequest(error);
            }

            var specialtyName = await ResolveSpecialtyNameAsync(db, norm.PrimarySpecialtyId, cancellationToken);
            if (specialtyName is null)
            {
                return Results.BadRequest($"Specialty '{norm.PrimarySpecialtyId}' does not exist.");
            }

            // Match past the soft-delete filter so the unique email index can't be violated: an
            // active email conflicts; a soft-deleted one is restored (and refreshed) instead of
            // inserting a duplicate.
            var existing = await db.Preceptors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Email == norm.Email, cancellationToken);

            if (existing is { IsDeleted: false })
            {
                return Results.Conflict($"A preceptor with email '{norm.Email}' already exists.");
            }

            if (existing is { IsDeleted: true })
            {
                existing.IsDeleted = false;
                existing.DeletedAtUtc = null;
                existing.DeletedBy = null;
                Apply(existing, norm);
                await db.SaveChangesAsync(cancellationToken);
                return Results.Created($"/api/preceptors/{existing.Id}", ToDetail(existing, specialtyName));
            }

            var preceptor = new Preceptor
            {
                FirstName = norm.FirstName,
                LastName = norm.LastName,
                Email = norm.Email,
                PrimarySpecialtyId = norm.PrimarySpecialtyId,
            };
            Apply(preceptor, norm);
            db.Preceptors.Add(preceptor);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
            {
                // Lost a concurrent create race for the same email; the unique index rejected the insert.
                return Results.Conflict($"A preceptor with email '{norm.Email}' already exists.");
            }

            return Results.Created($"/api/preceptors/{preceptor.Id}", ToDetail(preceptor, specialtyName));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("CreatePreceptor");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePreceptorRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryNormalize(request.FirstName, request.LastName, request.Email, request.PrimarySpecialtyId,
                    request.MedicalLicenseNumber, request.LicenseState, request.City, request.State,
                    request.Status, request.Bio, out var norm, out var error))
            {
                return Results.BadRequest(error);
            }

            var preceptor = await db.Preceptors.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (preceptor is null)
            {
                return Results.NotFound();
            }

            var specialtyName = await ResolveSpecialtyNameAsync(db, norm.PrimarySpecialtyId, cancellationToken);
            if (specialtyName is null)
            {
                return Results.BadRequest($"Specialty '{norm.PrimarySpecialtyId}' does not exist.");
            }

            // Email must be unique across every other preceptor (active or soft-deleted — the unique
            // index spans both), excluding this row.
            var emailTaken = await db.Preceptors
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Email == norm.Email && p.Id != id, cancellationToken);
            if (emailTaken)
            {
                return Results.Conflict($"A preceptor with email '{norm.Email}' already exists.");
            }

            Apply(preceptor, norm);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToDetail(preceptor, specialtyName));
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdatePreceptor");

        group.MapDelete("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var preceptor = await db.Preceptors.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (preceptor is null)
            {
                return Results.NotFound();
            }

            db.Preceptors.Remove(preceptor); // interceptor converts the delete into a soft-delete
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DeletePreceptor");

        return routes;
    }

    private const int NameMaxLength = 100;
    private const int EmailMaxLength = 256;
    private const int LicenseMaxLength = 50;
    private const int StateMaxLength = 50;
    private const int CityMaxLength = 100;
    private const int BioMaxLength = 4000;

    /// <summary>Validated, trimmed field values ready to persist.</summary>
    private sealed record NormalizedPreceptor(
        string FirstName, string LastName, string Email, Guid PrimarySpecialtyId,
        string? MedicalLicenseNumber, string? LicenseState, string? City, string? State,
        PreceptorStatus Status, string? Bio);

    private static Task<string?> ResolveSpecialtyNameAsync(RotationsDbContext db, Guid specialtyId, CancellationToken cancellationToken) =>
        db.Specialties
            .Where(s => s.Id == specialtyId)
            .Select(s => (string?)s.Name)
            .FirstOrDefaultAsync(cancellationToken);

    private static bool TryNormalize(
        string? firstName, string? lastName, string? email, Guid specialtyId,
        string? medicalLicenseNumber, string? licenseState, string? city, string? state,
        PreceptorStatus status, string? bio,
        out NormalizedPreceptor norm, out string error)
    {
        norm = null!;
        error = string.Empty;

        if (!TryRequired(firstName, NameMaxLength, "FirstName", out var first, out error)) return false;
        if (!TryRequired(lastName, NameMaxLength, "LastName", out var last, out error)) return false;
        if (!TryRequired(email, EmailMaxLength, "Email", out var mail, out error)) return false;
        if (!MailAddress.TryCreate(mail, out _))
        {
            error = "Email is not a valid address.";
            return false;
        }

        if (!Enum.IsDefined(status))
        {
            error = "Status is invalid.";
            return false;
        }

        if (!TryOptional(medicalLicenseNumber, LicenseMaxLength, "MedicalLicenseNumber", out var license, out error)) return false;
        if (!TryOptional(licenseState, StateMaxLength, "LicenseState", out var licState, out error)) return false;
        if (!TryOptional(city, CityMaxLength, "City", out var normalizedCity, out error)) return false;
        if (!TryOptional(state, StateMaxLength, "State", out var normalizedState, out error)) return false;
        if (!TryOptional(bio, BioMaxLength, "Bio", out var normalizedBio, out error)) return false;

        norm = new NormalizedPreceptor(first, last, mail, specialtyId, license, licState, normalizedCity, normalizedState, status, normalizedBio);
        return true;
    }

    private static bool TryRequired(string? input, int max, string field, out string value, out string error)
    {
        error = string.Empty;
        value = input?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            error = $"{field} is required.";
            return false;
        }

        if (value.Length > max)
        {
            error = $"{field} must be {max} characters or fewer.";
            return false;
        }

        return true;
    }

    private static bool TryOptional(string? input, int max, string field, out string? value, out string error)
    {
        error = string.Empty;
        value = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
        if (value is { } v && v.Length > max)
        {
            error = $"{field} must be {max} characters or fewer.";
            return false;
        }

        return true;
    }

    private static void Apply(Preceptor preceptor, NormalizedPreceptor norm)
    {
        preceptor.FirstName = norm.FirstName;
        preceptor.LastName = norm.LastName;
        preceptor.Email = norm.Email;
        preceptor.PrimarySpecialtyId = norm.PrimarySpecialtyId;
        preceptor.MedicalLicenseNumber = norm.MedicalLicenseNumber;
        preceptor.LicenseState = norm.LicenseState;
        preceptor.City = norm.City;
        preceptor.State = norm.State;
        preceptor.Status = norm.Status;
        preceptor.Bio = norm.Bio;
    }

    private static PreceptorDetailResponse ToDetail(Preceptor p, string specialtyName) =>
        new(p.Id, p.FirstName, p.LastName, p.Email, p.PrimarySpecialtyId, specialtyName,
            p.MedicalLicenseNumber, p.LicenseState, p.City, p.State, p.Status, p.Bio);
}
