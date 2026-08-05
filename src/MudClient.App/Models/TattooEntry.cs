namespace MudClient.App.Models;

public sealed record TattooCommandEntry(
    string Name,
    string Description);

public sealed record TattooRuneEntry(
    string Name,
    string Description);

public sealed record TattooBonusEntry(
    string Name,
    IReadOnlyList<string> Classes,
    string Description)
{
    public string ClassesText => Classes.Count == 0 ? "brak danych" : string.Join(", ", Classes);

    public string SearchableText => string.Join(' ', Name, ClassesText, Description);
}

public sealed record TattooCatalogData(
    string Intro,
    IReadOnlyList<TattooCommandEntry> Commands,
    IReadOnlyList<TattooRuneEntry> RuneTypes,
    string StackingNotes,
    IReadOnlyList<TattooBonusEntry> Bonuses);
