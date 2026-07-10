namespace MudClient.Core.Map;

public sealed class MapExit
{
    public required int ExitId { get; init; }

    public string? Name { get; init; }

    public string? Door { get; init; }

    public bool HasDoor => !string.IsNullOrWhiteSpace(Door);
}
