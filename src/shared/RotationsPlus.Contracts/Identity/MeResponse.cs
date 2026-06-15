namespace RotationsPlus.Contracts.Identity;

/// <summary>
/// Shape returned by GET /api/me — the authenticated staff identity plus the persisted profile
/// it was provisioned into. <see cref="ProfileId"/> and <see cref="LastSignInAtUtc"/> come from the
/// database (not the token), proving the full read/write round-trip on DEV.
/// </summary>
public sealed record MeResponse(
    string ObjectId,
    string? Name,
    string? Username,
    IReadOnlyList<string> Roles,
    bool IsStaff,
    Guid ProfileId,
    DateTimeOffset? LastSignInAtUtc);
