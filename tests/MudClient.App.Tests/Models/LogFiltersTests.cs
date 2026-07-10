using MudClient.App.Models;

namespace MudClient.App.Tests.Models;

public sealed class LogFiltersTests
{
    [Fact]
    public void Defaults_HasFourEntries()
    {
        Assert.Equal(4, LogFilters.Defaults.Count);
    }

    [Theory]
    [InlineData(0, "Wszystko", "all")]
    [InlineData(1, "Walka", "combat")]
    [InlineData(2, "Czaty", "chat")]
    [InlineData(3, "System", "system")]
    public void Defaults_ContainsExpectedFilter(int index, string expectedLabel, string expectedKey)
    {
        var filter = LogFilters.Defaults[index];
        Assert.Equal(expectedLabel, filter.Label);
        Assert.Equal(expectedKey, filter.Key);
    }

    [Fact]
    public void Defaults_IsReadOnly()
    {
        Assert.IsAssignableFrom<IReadOnlyList<LogFilter>>(LogFilters.Defaults);
    }

    [Fact]
    public void FilterRecord_Equality()
    {
        var a = new LogFilter("Walka", "combat");
        var b = new LogFilter("Walka", "combat");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void FilterRecord_Inequality()
    {
        var a = new LogFilter("Walka", "combat");
        var b = new LogFilter("System", "system");
        Assert.NotEqual(a, b);
    }
}
