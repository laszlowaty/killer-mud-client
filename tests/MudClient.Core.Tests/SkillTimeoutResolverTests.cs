using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class SkillTimeoutResolverTests
{
    private readonly SkillTimeoutResolver _resolver = new();

    [Fact]
    public void Process_SkillsTimeout_RaisesTimeoutsChangedWithAllEntries()
    {
        IReadOnlyList<SkillTimeoutEntry>? entries = null;
        _resolver.TimeoutsChanged += e => entries = e;

        _resolver.Process(new GmcpMessage(
            "Skills.Timeout",
            """{ "call avatar": { "timeout": true }, "torment": { "timeout": true } }"""));

        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Count);
        Assert.Contains(entries, e => e.Name == "call avatar" && e.Timeout);
        Assert.Contains(entries, e => e.Name == "torment" && e.Timeout);
    }

    [Fact]
    public void Process_SkillsTimeoutWithFalseFlag_ReportsNotOnCooldown()
    {
        IReadOnlyList<SkillTimeoutEntry>? entries = null;
        _resolver.TimeoutsChanged += e => entries = e;

        _resolver.Process(new GmcpMessage(
            "Skills.Timeout",
            """{ "torment": { "timeout": false } }"""));

        Assert.NotNull(entries);
        var entry = Assert.Single(entries!);
        Assert.Equal("torment", entry.Name);
        Assert.False(entry.Timeout);
    }

    [Fact]
    public void Process_UnrelatedPackage_DoesNotRaiseEvent()
    {
        var raised = false;
        _resolver.TimeoutsChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage("Char.Vitals", """{ "hp": 1 }"""));

        Assert.False(raised);
    }

    [Fact]
    public void Process_MalformedJson_IsIgnored()
    {
        var raised = false;
        _resolver.TimeoutsChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage("Skills.Timeout", "{ not json"));

        Assert.False(raised);
    }
}
