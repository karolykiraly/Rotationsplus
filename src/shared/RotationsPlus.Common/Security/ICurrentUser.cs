namespace RotationsPlus.Common.Security;

/// <summary>
/// Ambient accessor for the authenticated principal. Implemented in the API over IHttpContextAccessor;
/// faked in tests. Domain code depends on this rather than HttpContext.
/// </summary>
public interface ICurrentUser
{
    string? ObjectId { get; }
    string? Name { get; }
    string? Username { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
