namespace RotationsPlus.Common.Authorization;

/// <summary>Entra v2 token claim types this app reads.</summary>
public static class ClaimNames
{
    public const string ObjectId = "oid";
    public const string Roles = "roles";
    public const string Scope = "scp";
    public const string Name = "name";
    public const string PreferredUsername = "preferred_username";
}
