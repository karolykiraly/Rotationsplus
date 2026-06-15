using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Security;

namespace RotationsPlus.Api.Modules.Identity;

/// <summary>
/// Ensures the signed-in staff member has a local <see cref="StaffProfile"/>, creating it on first
/// sign-in and refreshing display name / email / last-seen on subsequent calls. This is what turns
/// the /api/me round-trip from "echoes the token" into "reads &amp; writes a persisted profile".
/// </summary>
public sealed class StaffProfileProvisioner(RotationsDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<StaffProfile> EnsureProvisionedAsync(CancellationToken cancellationToken = default)
    {
        var objectId = user.ObjectId
            ?? throw new InvalidOperationException("The current principal has no object id (oid) claim.");

        // Match on the unique business key past the soft-delete filter: a previously soft-deleted
        // profile must be reused/restored rather than collide with the unique index on EntraObjectId.
        var profile = await db.StaffProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.EntraObjectId == objectId, cancellationToken);

        if (profile is null)
        {
            profile = new StaffProfile { EntraObjectId = objectId };
            db.StaffProfiles.Add(profile);
        }

        Refresh(profile);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (db.Entry(profile).State == EntityState.Added)
        {
            // Lost a concurrent first-sign-in insert race: another request created the row first.
            // Reload the winner (it exists now) and apply this sign-in's refresh onto it.
            db.Entry(profile).State = EntityState.Detached;
            profile = await db.StaffProfiles
                .IgnoreQueryFilters()
                .FirstAsync(p => p.EntraObjectId == objectId, cancellationToken);
            Refresh(profile);
            await db.SaveChangesAsync(cancellationToken);
        }

        return profile;
    }

    /// <summary>Refresh mutable identity attributes from the token and restore if soft-deleted.</summary>
    private void Refresh(StaffProfile profile)
    {
        if (profile.IsDeleted)
        {
            profile.IsDeleted = false;
            profile.DeletedAtUtc = null;
            profile.DeletedBy = null;
        }

        profile.DisplayName = user.Name;
        profile.Email = user.Username;
        profile.LastSignInAtUtc = clock.GetUtcNow();
    }
}
