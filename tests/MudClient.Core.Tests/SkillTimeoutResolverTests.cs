using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class SkillTimeoutResolverTests
{
    private readonly SkillTimeoutResolver _resolver = new();

    [Fact]
    public void Process_CharSkillsTimeout_RaisesTimeoutsChangedWithAllEntries()
    {
        IReadOnlyList<SkillTimeoutEntry>? entries = null;
        _resolver.TimeoutsChanged += e => entries = e;

        _resolver.Process(new GmcpMessage(
            "Char.Skills.Timeout",
            """{ "call avatar": { "timeout": true }, "torment": { "timeout": true } }"""));

        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Count);
        Assert.Contains(entries, e => e.Name == "call avatar" && e.Timeout);
        Assert.Contains(entries, e => e.Name == "torment" && e.Timeout);
    }

    [Fact]
    public void Process_CharSkillsTimeoutWithFalseFlag_ReportsNotOnCooldown()
    {
        IReadOnlyList<SkillTimeoutEntry>? entries = null;
        _resolver.TimeoutsChanged += e => entries = e;

        _resolver.Process(new GmcpMessage(
            "Char.Skills.Timeout",
            """{ "torment": { "timeout": false } }"""));

        Assert.NotNull(entries);
        var entry = Assert.Single(entries!);
        Assert.Equal("torment", entry.Name);
        Assert.False(entry.Timeout);
    }

    [Theory]
    [InlineData("\"true\"", true)]
    [InlineData("\"false\"", false)]
    [InlineData("\"True\"", true)]
    public void Process_TimeoutAsStringValue_IsParsedAsBool(string rawValue, bool expected)
    {
        IReadOnlyList<SkillTimeoutEntry>? entries = null;
        _resolver.TimeoutsChanged += e => entries = e;

        _resolver.Process(new GmcpMessage(
            "Char.Skills.Timeout",
            $$"""{ "holy prayer": { "timeout": {{rawValue}} } }"""));

        var entry = Assert.Single(entries!);
        Assert.Equal("holy prayer", entry.Name);
        Assert.Equal(expected, entry.Timeout);
    }

    [Fact]
    public void Process_PackageNameIsCaseInsensitive()
    {
        IReadOnlyList<SkillTimeoutEntry>? entries = null;
        _resolver.TimeoutsChanged += e => entries = e;

        _resolver.Process(new GmcpMessage(
            "char.skills.timeout",
            """{ "holy prayer": { "timeout": "true" } }"""));

        var entry = Assert.Single(entries!);
        Assert.Equal("holy prayer", entry.Name);
        Assert.True(entry.Timeout);
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

        _resolver.Process(new GmcpMessage("Char.Skills.Timeout", "{ not json"));

        Assert.False(raised);
    }
}
