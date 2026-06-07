namespace Valkompass.Domain.Identity;

/// <summary>Rollnamn för admingränssnittets behörigheter.</summary>
public static class Roles
{
    /// <summary>Full behörighet, inklusive att ta bort innehåll och hantera användare.</summary>
    public const string Admin = "Admin";

    /// <summary>Redaktör som kan skapa och redigera frågor, partier och positioner.</summary>
    public const string Editor = "Editor";

    public static readonly string[] All = [Admin, Editor];
}
