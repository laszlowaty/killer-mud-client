namespace MudClient.App.Models;

/// <summary>Region świata dostępny w killeropedii jako statyczna mapa (PNG).</summary>
public sealed record WorldMapRegion(string Name, string ImageFileName)
{
    public string ImagePath => Path.Combine(
        AppContext.BaseDirectory, "Assets", "Map", "Locations", ImageFileName);
}
