namespace MudClient.App.Services;

/// <summary>
/// Compile-time switches for creator-only maintenance tools. Enabling refresh still requires
/// an active MUD connection; normal builds only read the generated books JSON.
/// </summary>
internal static class DeveloperFeatures
{
    public const bool ShowBookCatalogRefreshButton = true;

    public const bool EnableBookCatalogRefreshButton = false;

    /// <summary>
    /// Set to an explicit path when a development build should write directly to a repository
    /// snapshot. Null writes to %AppData%/KillerMudClient/killeropedia-books.json.
    /// </summary>
    public static string? BookCatalogOutputPath => null;

    public const bool ShowRareCatalogRefreshButton = true;

    // Enabled by default (unlike the book refresh above): the embedded rares.json only has the
    // rarelist item/vnum listing, not the per-vnum "rarelist <vnum>" details — those can only be
    // captured by running this while connected, so the button needs to be usable out of the box.
    public const bool EnableRareCatalogRefreshButton = true;

    /// <summary>
    /// Set to an explicit path when a development build should write directly to a repository
    /// snapshot. Null writes to %AppData%/KillerMudClient/killeropedia-rares.json.
    /// </summary>
    public static string? RareCatalogOutputPath => null;
}
