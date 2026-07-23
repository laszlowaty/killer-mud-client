namespace MudClient.App.Models;

public sealed record QuestEntry(
    string Name,
    string Region,
    string Giver)
{
    public string SearchableText => string.Join(' ', Name, Region, Giver);
}
