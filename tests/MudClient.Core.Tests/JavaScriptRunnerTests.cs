using System.Globalization;
using System.Text.Json;
using MudClient.Core.Scripting;

namespace MudClient.Core.Tests;

public sealed class JavaScriptRunnerTests
{
    [Fact]
    public async Task Execute_IfAndMatch_ProduceOrderedEffects()
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

        var result = await ExecuteAsync(runner, invocation, variables);

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
    public async Task Execute_Variables_AreSharedAcrossInvocations()
    {
        var runner = new JavaScriptRunner();
        var variables = new TestVariableStore();

        var first = await ExecuteAsync(
            runner,
            new ScriptInvocation(
                "zapis",
                "trigger",
                """
                variables.set("combat.target", "ork");
                variables.increment("combat.seen");
                """),
            variables);
        var second = await ExecuteAsync(
            runner,
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
    public async Task Execute_OnGmcp_FiltersPackageAndExposesJsonData()
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

        var result = await ExecuteAsync(runner, invocation, variables);

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            new ScriptEffect(ScriptEffectKind.Execute, "uciekaj"),
            Assert.Single(result.Effects));
    }

    [Fact]
    public async Task Execute_LogAndConsole_WriteDedicatedLogEffects()
    {
        var runner = new JavaScriptRunner();
        var result = await ExecuteAsync(
            runner,
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

    [Theory]
    [InlineData("alias")]
    [InlineData("trigger")]
    [InlineData("timer")]
    [InlineData("script")]
    public async Task Execute_Reconnect_ProducesReconnectEffectForEveryAutomationSource(string source)
    {
        var runner = new JavaScriptRunner();

        var result = await ExecuteAsync(
            runner,
            new ScriptInvocation("ponowne połączenie", source, "reconnect();"),
            new TestVariableStore());

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            new ScriptEffect(ScriptEffectKind.Reconnect, string.Empty),
            Assert.Single(result.Effects));
    }

    [Theory]
    [InlineData("alias")]
    [InlineData("trigger")]
    [InlineData("timer")]
    [InlineData("script")]
    public async Task Execute_DeleteLine_ProducesSharedEffect(string source)
    {
        var result = await ExecuteAsync(
            new JavaScriptRunner(),
            new ScriptInvocation("ukrywanie", source, "deleteLine();"),
            new TestVariableStore());

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            new ScriptEffect(ScriptEffectKind.DeleteLine, string.Empty),
            Assert.Single(result.Effects));
    }

    [Fact]
    public async Task Execute_InfiniteLoop_IsStopped()
    {
        var runner = new JavaScriptRunner();
        var result = await ExecuteAsync(
            runner,
            new ScriptInvocation("pętla", "script", "while (true) {}"),
            new TestVariableStore());

        Assert.False(result.Success);
        Assert.Contains("pętla", result.Error);
    }

    [Fact]
    public async Task Execute_DoesNotExposeClr()
    {
        var runner = new JavaScriptRunner();
        var result = await ExecuteAsync(
            runner,
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

    [Fact]
    public void Validate_AllowsAwaitedHttpRequest()
    {
        var error = new JavaScriptRunner().Validate(
            "http",
            """const response = await http.get("https://example.com/");""");

        Assert.Null(error);
    }

    [Fact]
    public async Task ExecuteAsync_HttpGet_CanBeAwaitedAndParsed()
    {
        var runner = new JavaScriptRunner();
        ScriptHttpRequest? capturedRequest = null;
        var httpClient = new TestHttpClient(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            await Task.Delay(350, cancellationToken);
            return new ScriptHttpResponse(
                200,
                "OK",
                request.Url,
                new Dictionary<string, string> { ["content-type"] = "application/json" },
                """{"name":"Killer"}""");
        });

        var result = await runner.ExecuteAsync(
            new ScriptInvocation(
                "request",
                "script",
                """
                const response = await http.get("https://example.com/data", {
                    headers: { "X-Test": "tak" },
                    timeoutMs: 2000
                });
                const data = response.json();
                echo(response.status + ":" + response.ok + ":" + data.name);
                """),
            new TestVariableStore(),
            httpClient);

        Assert.True(result.Success, result.Error);
        Assert.Equal("GET", capturedRequest?.Method);
        Assert.Equal("tak", capturedRequest?.Headers["X-Test"]);
        Assert.Equal(2000, capturedRequest?.TimeoutMilliseconds);
        Assert.Equal(
            new ScriptEffect(ScriptEffectKind.Echo, "200:true:Killer", "cyan"),
            Assert.Single(result.Effects));
    }

    [Fact]
    public async Task ExecuteAsync_HttpPost_SerializesObjectBodyAsJson()
    {
        ScriptHttpRequest? capturedRequest = null;
        var httpClient = new TestHttpClient((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new ScriptHttpResponse(
                204,
                "No Content",
                request.Url,
                new Dictionary<string, string>(),
                string.Empty));
        });

        var result = await new JavaScriptRunner().ExecuteAsync(
            new ScriptInvocation(
                "post",
                "script",
                """await http.post("https://example.com/hook", { hp: 42 });"""),
            new TestVariableStore(),
            httpClient);

        Assert.True(result.Success, result.Error);
        Assert.Equal("POST", capturedRequest?.Method);
        Assert.Equal("{\"hp\":42}", capturedRequest?.Body);
        Assert.Equal("application/json", capturedRequest?.Headers["Content-Type"]);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequest_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var httpClient = new TestHttpClient(async (_, cancellationToken) =>
        {
            requestStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Nieosiągalne po anulowaniu.");
        });
        var execution = new JavaScriptRunner().ExecuteAsync(
            new ScriptInvocation(
                "anulowanie",
                "script",
                """await http.get("https://example.com/");"""),
            new TestVariableStore(),
            httpClient,
            cancellation.Token);

        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequest_StopsAfterConfiguredRequestLimit()
    {
        var requestCount = 0;
        var httpClient = new TestHttpClient((request, _) =>
        {
            requestCount++;
            return Task.FromResult(new ScriptHttpResponse(
                200,
                "OK",
                request.Url,
                new Dictionary<string, string>(),
                string.Empty));
        });

        var result = await new JavaScriptRunner().ExecuteAsync(
            new ScriptInvocation(
                "limit",
                "script",
                """
                for (let index = 0; index < 6; index++) {
                    await http.get("https://example.com/" + index);
                }
                """),
            new TestVariableStore(),
            httpClient);

        Assert.False(result.Success);
        Assert.Equal(JavaScriptRunner.MaximumHttpRequests, requestCount);
        Assert.Contains("requestów HTTP", result.Error);
    }

    private static Task<ScriptExecutionResult> ExecuteAsync(
        JavaScriptRunner runner,
        ScriptInvocation invocation,
        IScriptVariableStore variables) =>
        runner.ExecuteAsync(invocation, variables, new TestHttpClient());

    private sealed class TestHttpClient : IScriptHttpClient
    {
        private readonly Func<ScriptHttpRequest, CancellationToken, Task<ScriptHttpResponse>>? _send;

        public TestHttpClient(
            Func<ScriptHttpRequest, CancellationToken, Task<ScriptHttpResponse>>? send = null)
        {
            _send = send;
        }

        public Task<ScriptHttpResponse> SendAsync(
            ScriptHttpRequest request,
            CancellationToken cancellationToken) =>
            _send?.Invoke(request, cancellationToken)
            ?? throw new InvalidOperationException("Test nie oczekiwał requestu HTTP.");
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
