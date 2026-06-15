namespace RotationsPlus.Contracts.Identity;

/// <summary>
/// Shape returned by GET /api/me — the authenticated staff identity. This is the P1 Entra
/// login round-trip payload the SPA renders to prove end-to-end auth on DEV.
/// </summary>
public sealed record MeResponse(
    string ObjectId,
    string? Name,
    string? Username,
    IReadOnlyList<string> Roles,
    bool IsStaff);
