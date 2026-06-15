using RotationsPlus.Common.Domain;

namespace RotationsPlus.Api.Modules.Identity;

/// <summary>
/// A staff member's domain profile, provisioned on first sign-in and keyed to their workforce
/// Entra object id (<c>oid</c>). Roles themselves live in Entra tokens, not here — this is the
/// local record that domain rows (rotations, leads, …) reference and that audit trails point at.
/// </summary>
public sealed class StaffProfile : AuditableEntity
{
    /// <summary>The workforce Entra <c>oid</c> claim — the stable identity key.</summary>
    public required string EntraObjectId { get; set; }

    /// <summary>Display name from the token (<c>name</c> claim); may be refreshed on later sign-ins.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Sign-in name from the token (<c>preferred_username</c>/UPN); blank for guest accounts.</summary>
    public string? Email { get; set; }

    /// <summary>Last time this profile was seen on a /api/me call.</summary>
    public DateTimeOffset? LastSignInAtUtc { get; set; }
}
