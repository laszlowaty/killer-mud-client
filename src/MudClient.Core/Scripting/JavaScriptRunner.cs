using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;

namespace MudClient.Core.Scripting;

/// <summary>
/// Executes user JavaScript in a constrained Jint interpreter. CLR access is
/// intentionally not enabled; only explicitly registered delegates are
/// visible to scripts.
/// </summary>
public sealed class JavaScriptRunner
{
    public const int MaximumEffects = 100;
    public const int MaximumEffectTextLength = 16_384;
    public const int MaximumVariableJsonLength = 262_144;

    private static readonly Regex VariableNameRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_.-]{0,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromMilliseconds(250);

    public string? Validate(string name, string code)
    {
        try
        {
            _ = Engine.PrepareScript(BuildProgram(code), name);
            return null;
        }
        catch (Exception exception)
        {
            return FormatError(name, exception);
        }
    }

    public ScriptExecutionResult Execute(
        ScriptInvocation invocation,
        IScriptVariableStore variables,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(variables);

        var effects = new List<ScriptEffect>();

        try
        {
            var engine = new Engine(options =>
            {
                options.Strict();
                options.LimitMemory(16_000_000);
                options.TimeoutInterval(ExecutionTimeout);
                options.MaxStatements(20_000);
                options.LimitRecursion(64);
                options.CancellationToken(cancellationToken);
            });

            engine.SetValue("__contextJson", BuildContextJson(invocation));
            engine.SetValue("__getVariable", new Func<string, string?, string>(GetVariable));
            engine.SetValue("__setVariable", new Action<string, string>(SetVariable));
            engine.SetValue("__hasVariable", new Func<string, bool>(HasVariable));
            engine.SetValue("__removeVariable", new Func<string, bool>(RemoveVariable));
            engine.SetValue("__incrementVariable", new Func<string, double, double>(IncrementVariable));
            engine.SetValue("__addEffect", new Action<string, string, string?>(AddEffect));
            engine.SetValue("__gmcpMatches", new Func<string, string, bool>(MatchesGmcpPackage));

            engine.Execute(BuildProgram(invocation.Code));
            return new ScriptExecutionResult(effects);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ScriptExecutionResult(effects, FormatError(invocation.Name, exception));
        }

        string GetVariable(string name, string? fallbackJson)
        {
            ValidateVariableName(name);
            return variables.GetJson(name)
                   ?? NormalizeJson(fallbackJson ?? "null");
        }

        void SetVariable(string name, string json)
        {
            ValidateVariableName(name);
            variables.SetJson(name, NormalizeJson(json));
        }

        bool HasVariable(string name)
        {
            ValidateVariableName(name);
            return variables.Contains(name);
        }

        bool RemoveVariable(string name)
        {
            ValidateVariableName(name);
            return variables.Remove(name);
        }

        double IncrementVariable(string name, double amount)
        {
            ValidateVariableName(name);
            if (!double.IsFinite(amount))
            {
                throw new ArgumentException("Wartość zwiększenia musi być skończoną liczbą.");
            }

            return variables.Increment(name, amount);
        }

        void AddEffect(string kind, string text, string? color)
        {
            if (effects.Count >= MaximumEffects)
            {
                throw new InvalidOperationException(
                    $"Skrypt może utworzyć najwyżej {MaximumEffects} akcji.");
            }

            if (text.Length > MaximumEffectTextLength)
            {
                throw new InvalidOperationException(
                    $"Pojedyncza akcja może mieć najwyżej {MaximumEffectTextLength} znaków.");
            }

            var effectKind = kind switch
            {
                "execute" => ScriptEffectKind.Execute,
                "send" => ScriptEffectKind.Send,
                "echo" => ScriptEffectKind.Echo,
                "log" => ScriptEffectKind.Log,
                _ => throw new ArgumentException($"Nieznany rodzaj akcji skryptu: {kind}."),
            };

            effects.Add(new ScriptEffect(effectKind, text, color));
        }
    }

    private static string BuildContextJson(ScriptInvocation invocation)
    {
        object? eventContext = invocation.Gmcp is null
            ? null
            : new
            {
                type = "gmcp",
                package = invocation.Gmcp.Package,
                data = ParseJsonValue(invocation.Gmcp.Json),
                raw = invocation.Gmcp.Json,
            };

        var context = new
        {
            source = invocation.Source,
            input = invocation.Input,
            match = invocation.Match is null
                ? null
                : new
                {
                    value = invocation.Match.Value,
                    captures = invocation.Match.Captures,
                    groups = invocation.Match.Groups,
                },
            @event = eventContext,
        };

        return JsonSerializer.Serialize(context);
    }

    private static object? ParseJsonValue(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string BuildProgram(string userCode) => $$"""
        "use strict";
        const __context = JSON.parse(__contextJson);
        const input = __context.input;
        const match = __context.match;
        const event = __context.event;

        const variables = Object.freeze({
            get(name, fallbackValue = null) {
                return JSON.parse(__getVariable(String(name), JSON.stringify(fallbackValue)));
            },
            set(name, value) {
                __setVariable(String(name), JSON.stringify(value));
                return value;
            },
            has(name) {
                return __hasVariable(String(name));
            },
            remove(name) {
                return __removeVariable(String(name));
            },
            increment(name, amount = 1) {
                return __incrementVariable(String(name), Number(amount));
            }
        });

        function execute(text) {
            __addEffect("execute", String(text), null);
        }

        function send(text) {
            __addEffect("send", String(text), null);
        }

        function runAlias(text) {
            __addEffect("execute", String(text), null);
        }

        function echo(text, color = "cyan") {
            __addEffect("echo", String(text), String(color));
        }

        function __formatLogValues(values) {
            return values.map(value => {
                if (typeof value === "string") {
                    return value;
                }

                try {
                    const json = JSON.stringify(value);
                    return json === undefined ? String(value) : json;
                } catch {
                    return String(value);
                }
            }).join(" ");
        }

        function log(...values) {
            __addEffect("log", __formatLogValues(values), "info");
        }

        const console = Object.freeze({
            log(...values) {
                __addEffect("log", __formatLogValues(values), "info");
            },
            warn(...values) {
                __addEffect("log", __formatLogValues(values), "warning");
            },
            error(...values) {
                __addEffect("log", __formatLogValues(values), "error");
            }
        });

        function onGmcp(pattern, handler) {
            if (event && event.type === "gmcp"
                && __gmcpMatches(String(pattern), event.package)) {
                handler(event);
            }
        }

        (() => {
        {{userCode}}
        })();
        """;

    private static string NormalizeJson(string json)
    {
        if (json.Length > MaximumVariableJsonLength)
        {
            throw new InvalidOperationException(
                $"Wartość zmiennej może mieć najwyżej {MaximumVariableJsonLength} znaków JSON.");
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetRawText();
    }

    private static void ValidateVariableName(string name)
    {
        if (!VariableNameRegex.IsMatch(name))
        {
            throw new ArgumentException(
                "Nazwa zmiennej musi zaczynać się literą lub _, mieć do 128 znaków "
                + "i zawierać tylko litery, cyfry, _, -, lub kropki.");
        }
    }

    public static bool MatchesGmcpPackage(string pattern, string package)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        if (pattern == "*")
        {
            return true;
        }

        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefix = pattern[..^1];
            return package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(pattern, package, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatError(string scriptName, Exception exception)
    {
        var message = exception.Message.Replace("\r", " ").Replace("\n", " ").Trim();
        return $"Skrypt „{scriptName}”: {message}";
    }
}
