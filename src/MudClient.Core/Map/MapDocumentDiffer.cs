namespace MudClient.Core.Map;

public sealed record MapDocumentDiff(
    int AddedAreas,
    int RemovedAreas,
    int ChangedAreas,
    int AddedRooms,
    int RemovedRooms,
    int ChangedRooms,
    int AddedLabels,
    int RemovedLabels,
    int ChangedLabels)
{
    public bool HasChanges =>
        AddedAreas + RemovedAreas + ChangedAreas +
        AddedRooms + RemovedRooms + ChangedRooms +
        AddedLabels + RemovedLabels + ChangedLabels > 0;

    public string ToPolishSummary() => HasChanges
        ? $"Różnice względem mapy bazowej: obszary +{AddedAreas}/-{RemovedAreas}/~{ChangedAreas}, " +
          $"pokoje +{AddedRooms}/-{RemovedRooms}/~{ChangedRooms}, " +
          $"etykiety +{AddedLabels}/-{RemovedLabels}/~{ChangedLabels}."
        : "Mapa robocza nie różni się od mapy bazowej.";
}

public static class MapDocumentDiffer
{
    public static MapDocumentDiff Compare(MapDocument baseline, MapDocument edited)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(edited);

        var baselineAreas = baseline.Areas.GroupBy(area => area.Id).ToDictionary(group => group.Key, group => group.Last());
        var editedAreas = edited.Areas.GroupBy(area => area.Id).ToDictionary(group => group.Key, group => group.Last());
        var baselineRooms = baseline.Areas.SelectMany(area => area.Rooms)
            .GroupBy(room => room.Id).ToDictionary(group => group.Key, group => group.Last());
        var editedRooms = edited.Areas.SelectMany(area => area.Rooms)
            .GroupBy(room => room.Id).ToDictionary(group => group.Key, group => group.Last());
        var baselineLabels = baseline.Areas.SelectMany(area => area.Labels)
            .GroupBy(label => label.Id).ToDictionary(group => group.Key, group => group.Last());
        var editedLabels = edited.Areas.SelectMany(area => area.Labels)
            .GroupBy(label => label.Id).ToDictionary(group => group.Key, group => group.Last());

        return new MapDocumentDiff(
            AddedAreas: editedAreas.Keys.Except(baselineAreas.Keys).Count(),
            RemovedAreas: baselineAreas.Keys.Except(editedAreas.Keys).Count(),
            ChangedAreas: SharedKeys(baselineAreas, editedAreas)
                .Count(id => !string.Equals(baselineAreas[id].Name, editedAreas[id].Name, StringComparison.Ordinal)),
            AddedRooms: editedRooms.Keys.Except(baselineRooms.Keys).Count(),
            RemovedRooms: baselineRooms.Keys.Except(editedRooms.Keys).Count(),
            ChangedRooms: SharedKeys(baselineRooms, editedRooms)
                .Count(id => !RoomsEqual(baselineRooms[id], editedRooms[id])),
            AddedLabels: editedLabels.Keys.Except(baselineLabels.Keys).Count(),
            RemovedLabels: baselineLabels.Keys.Except(editedLabels.Keys).Count(),
            ChangedLabels: SharedKeys(baselineLabels, editedLabels)
                .Count(id => !LabelsEqual(baselineLabels[id], editedLabels[id])));
    }

    private static IEnumerable<int> SharedKeys<T>(
        IReadOnlyDictionary<int, T> left,
        IReadOnlyDictionary<int, T> right) => left.Keys.Intersect(right.Keys);

    private static bool RoomsEqual(MapRoom left, MapRoom right) =>
        left.AreaId == right.AreaId &&
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        left.Coordinates == right.Coordinates &&
        left.Environment == right.Environment &&
        left.Weight == right.Weight &&
        string.Equals(left.Symbol, right.Symbol, StringComparison.Ordinal) &&
        left.Exits.OrderBy(ExitKey).Select(ExitKey).SequenceEqual(right.Exits.OrderBy(ExitKey).Select(ExitKey)) &&
        UserDataEqual(left.UserData, right.UserData);

    private static bool LabelsEqual(MapLabel left, MapLabel right) =>
        left.AreaId == right.AreaId &&
        string.Equals(left.Text, right.Text, StringComparison.Ordinal) &&
        left.Coordinates == right.Coordinates &&
        left.FontSize == right.FontSize &&
        left.ShowOnTop == right.ShowOnTop;

    private static string ExitKey(MapExit exit) => $"{exit.ExitId}\u001f{exit.Name}\u001f{exit.Door}";

    private static bool UserDataEqual(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement>? left,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement>? right)
    {
        if ((left?.Count ?? 0) != (right?.Count ?? 0))
        {
            return false;
        }

        if (left is null || right is null)
        {
            return true;
        }

        return left.All(pair =>
            right.TryGetValue(pair.Key, out var value) &&
            string.Equals(pair.Value.GetRawText(), value.GetRawText(), StringComparison.Ordinal));
    }
}
