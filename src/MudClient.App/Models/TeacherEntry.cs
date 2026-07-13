namespace MudClient.App.Models;

public sealed record TeacherSkillEntry(
    string Name,
    int Min,
    int? Max,
    int RequiredSkill,
    int Price)
{
    public string RangeText => Max is null ? $"od {Min}" : $"{Min}–{Max}";

    public string PriceText => $"{Price}%";

    public string RequirementText => $"od {RequiredSkill}";
}

public sealed record TeacherEntry(
    string MobVnum,
    string Name,
    string Region,
    string? Area,
    string? RoomVnum,
    IReadOnlyList<string> Classes,
    IReadOnlyList<TeacherSkillEntry> Skills)
{
    public string LocationText => string.IsNullOrWhiteSpace(Area) ? Region : Area;

    public string RegionText => string.IsNullOrWhiteSpace(Area) || Area == Region
        ? Region
        : $"{Area} · region {Region}";

    public string RoomText => string.IsNullOrWhiteSpace(RoomVnum) ? "brak danych" : RoomVnum;

    public string ClassesText => Classes.Count == 0 ? "brak danych" : string.Join(", ", Classes);

    public string SkillCountText => $"{Skills.Count} umiejętności";
}
