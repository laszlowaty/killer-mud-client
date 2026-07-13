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
}
