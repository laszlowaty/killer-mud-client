using MudClient.Core.Map;

namespace MudClient.App.ViewModels;

/// <summary>A group member positioned in a map room resolved from Char.Group GMCP.</summary>
public sealed record GroupMapMarker(string Name, bool IsLeader, MapRoom Room, int Number)
{
    public string GetLabel(bool showAsNumber) => showAsNumber
        ? Number.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Name;
}
