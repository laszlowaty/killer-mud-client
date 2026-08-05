using System.Globalization;
using System.Text.Json;
using MudClient.Core.Scripting;

namespace MudClient.Core.Tests;

public sealed class JavaScriptRunnerTests
{
    [Fact]
    public void Execute_IfAndMatch_ProduceOrderedEffects()
    {
        var runner = new JavaScriptRunner();
        var variables = new TestVariableStore();
        var invocation = new ScriptInvocation(
            "mocny cios",
            "trigger",
            """
            if (Number(match.groups.damage) > 50) {
                echo("Mocne uderzenie", "red");
                execute("/idz lecznica");
                send("uciekaj");
            }
            """,
            Input: "Cios za 75",
            Match: new ScriptMatchContext(
                "Cios za 75",
                ["Cios za 75", "75"],
                new Dictionary<string, string> { ["damage"] = "75" }));

        var result = runner.Execute(invocation, variables);

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            [
                new ScriptEffect(ScriptEffectKind.Echo, "Mocne uderzenie", "red"),
                new ScriptEffect(ScriptEffectKind.Execute, "/idz lecznica"),
                new ScriptEffect(ScriptEffectKind.Send, "uciekaj"),
            ],
            result.Effects);
    }

    [Fact]
    public void Execute_Variables_AreSharedAcrossInvocations()
    {
        var runner = new JavaScriptRunner();
        var variables = new TestVariableStore();

        var first = runner.Execute(
            new ScriptInvocation(
                "zapis",
                "trigger",
                """
                variables.set("combat.target", "ork");
                variables.increment("combat.seen");
                """),
            variables);
        var second = runner.Execute(
            new ScriptInvocation(
                "odczyt",
                "timer",
                """
                echo(variables.get("combat.target") + ":" + variables.get("combat.seen"));
                """),
            variables);

        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);
        Assert.Equal(
            new ScriptEffect(ScriptEffectKind.Echo, "ork:1", "cyan"),
            Assert.Single(second.Effects));
    }

    [Fact]
    public void Execute_OnGmcp_FiltersPackageAndExposesJsonData()
    {
        var runner = new JavaScriptRunner();
        var variables = new TestVariableStore();
        var invocation = new ScriptInvocation(
            "niskie hp",
            "script",
            """
            onGmcp("Char.*", message => {
                if (message.data.hp < 25) {
                    execute("uciekaj");
                }
            });
            """,
            Gmcp: new ScriptGmcpContext("Char.Vitals", """{"hp":20,"maxhp":100}"""));

        var result = runner.Execute(invocation, variables);

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            new ScriptEffect(ScriptEffectKind.Execute, "uciekaj"),
            Assert.Single(result.Effects));
    }

    [Fact]
    public void Execute_LogAndConsole_WriteDedicatedLogEffects()
    {
        var runner = new JavaScriptRunner();
        var result = runner.Execute(
            new ScriptInvocation(
                "diagnostyka",
                "script",
                """
                log("start", { hp: 42 });
                console.log("cel", "ork");
                console.warn("mało many");
                console.error("awaria");
                """),
            new TestVariableStore());

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            [
                new ScriptEffect(ScriptEffectKind.Log, """start {"hp":42}""", "info"),
                new ScriptEffect(ScriptEffectKind.Log, "cel ork", "info"),
                new ScriptEffect(ScriptEffectKind.Log, "mało many", "warning"),
                new ScriptEffect(ScriptEffectKind.Log, "awaria", "error"),
            ],
            result.Effects);
    }

    [Fact]
    public void Execute_InfiniteLoop_IsStopped()
    {
        var runner = new JavaScriptRunner();
        var result = runner.Execute(
            new ScriptInvocation("pętla", "script", "while (true) {}"),
            new TestVariableStore());

        Assert.False(result.Success);
        Assert.Contains("pętla", result.Error);
    }

    [Fact]
    public void Execute_DoesNotExposeClr()
    {
        var runner = new JavaScriptRunner();
        var result = runner.Execute(
            new ScriptInvocation(
                "clr",
                "script",
                """execute(typeof System + ":" + typeof importNamespace);"""),
            new TestVariableStore());

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            new ScriptEffect(ScriptEffectKind.Execute, "undefined:undefined"),
            Assert.Single(result.Effects));
    }

    [Fact]
    public void Validate_ReportsSyntaxErrorWithoutExecutingCode()
    {
        var runner = new JavaScriptRunner();

        var error = runner.Validate("błędny", "if (");

        Assert.Contains("błędny", error);
    }

    private sealed class TestVariableStore : IScriptVariableStore
    {
        private readonly Dictionary<string, string> _values =
            new(StringComparer.OrdinalIgnoreCase);

        public string? GetJson(string name) =>
            _values.TryGetValue(name, out var value) ? value : null;

        public void SetJson(string name, string json) => _values[name] = json;

        public bool Contains(string name) => _values.ContainsKey(name);

        public bool Remove(string name) => _values.Remove(name);

        public double Increment(string name, double amount)
        {
            var current = 0d;
            if (_values.TryGetValue(name, out var json))
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Number
                    || !document.RootElement.TryGetDouble(out current))
                {
                    throw new InvalidOperationException("Zmienna nie jest liczbą.");
                }
            }

            var next = current + amount;
            _values[name] = next.ToString("R", CultureInfo.InvariantCulture);
            return next;
        }
    }
}
