using System.Text.Json;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Map;

public sealed class GmcpLocationResolver
{
    private readonly MapGmcpLocationSettings _settings;

    public GmcpLocationResolver(MapGmcpLocationSettings? settings = null)
    {
        _settings = settings ?? new MapGmcpLocationSettings();
    }

    public event Action<string>? LocationChanged;

    public string? CurrentVnum { get; private set; }

    public void Process(GmcpMessage message)
    {
        if (string.IsNullOrEmpty(message.Package))
        {
            return;
        }

        var matchesPackage = _settings.Packages.Any(
            package => string.Equals(package, message.Package, StringComparison.OrdinalIgnoreCase));

        if (!matchesPackage || string.IsNullOrWhiteSpace(message.Json))
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(message.Json);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            foreach (var path in _settings.VnumPaths)
            {
                if (!TryResolvePath(document.RootElement, path, out var vnum))
                {
                    continue;
                }

                if (!string.Equals(vnum, CurrentVnum, StringComparison.Ordinal))
                {
                    CurrentVnum = vnum;
                    LocationChanged?.Invoke(vnum);
                }

                return;
            }
        }
    }

    private static bool TryResolvePath(JsonElement root, string path, out string vnum)
    {
        vnum = string.Empty;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return false;
            }

            current = next;
        }

        switch (current.ValueKind)
        {
            case JsonValueKind.String:
                vnum = current.GetString() ?? string.Empty;
                return vnum.Length > 0;

            case JsonValueKind.Number:
                vnum = current.ToString();
                return true;

            default:
                return false;
        }
    }
}
