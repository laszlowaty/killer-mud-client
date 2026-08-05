using System.Globalization;
using System.Text.Json;
using MudClient.Core.Scripting;

namespace MudClient.App.Services;

public sealed class ProfileScriptVariableStore : IScriptVariableStore
{
    private readonly object _sync = new();
    private readonly Action _changed;
    private Dictionary<string, JsonElement> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public ProfileScriptVariableStore(Action changed)
    {
        _changed = changed;
    }

    public void Replace(IReadOnlyDictionary<string, JsonElement>? values)
    {
        lock (_sync)
        {
            _values = values?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public Dictionary<string, JsonElement> Snapshot()
    {
        lock (_sync)
        {
            return _values.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public string? GetJson(string name)
    {
        lock (_sync)
        {
            return _values.TryGetValue(name, out var value)
                ? value.GetRawText()
                : null;
        }
    }

    public void SetJson(string name, string json)
    {
        using var document = JsonDocument.Parse(json);
        lock (_sync)
        {
            _values[name] = document.RootElement.Clone();
        }

        _changed();
    }

    public bool Contains(string name)
    {
        lock (_sync)
        {
            return _values.ContainsKey(name);
        }
    }

    public bool Remove(string name)
    {
        bool removed;
        lock (_sync)
        {
            removed = _values.Remove(name);
        }

        if (removed)
        {
            _changed();
        }

        return removed;
    }

    public double Increment(string name, double amount)
    {
        double next;
        lock (_sync)
        {
            var current = 0d;
            if (_values.TryGetValue(name, out var value))
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out current))
                {
                    throw new InvalidOperationException($"Zmienna „{name}” nie jest liczbą.");
                }
            }

            next = current + amount;
            if (!double.IsFinite(next))
            {
                throw new InvalidOperationException("Wynik operacji na zmiennej nie jest skończoną liczbą.");
            }

            using var document = JsonDocument.Parse(
                next.ToString("R", CultureInfo.InvariantCulture));
            _values[name] = document.RootElement.Clone();
        }

        _changed();
        return next;
    }
}
