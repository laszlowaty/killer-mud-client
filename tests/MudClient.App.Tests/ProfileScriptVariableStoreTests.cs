using System.Text.Json;
using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class ProfileScriptVariableStoreTests
{
    [Fact]
    public void Values_AreJsonCaseInsensitiveAndIncrementIsAtomic()
    {
        var changes = 0;
        var store = new ProfileScriptVariableStore(() => changes++);

        store.SetJson("Combat.Count", "2");
        var next = store.Increment("combat.count", 3);
        store.SetJson("combat.target", "\"ork\"");

        Assert.Equal(5, next);
        Assert.Equal("5", store.GetJson("COMBAT.COUNT"));
        Assert.Equal("\"ork\"", store.GetJson("Combat.Target"));
        Assert.Equal(3, changes);
    }

    [Fact]
    public void ReplaceAndSnapshot_CloneJsonValues()
    {
        using var document = JsonDocument.Parse("""{"name":"ork"}""");
        var store = new ProfileScriptVariableStore(() => { });
        store.Replace(new Dictionary<string, JsonElement>
        {
            ["target"] = document.RootElement.Clone(),
        });

        var snapshot = store.Snapshot();

        Assert.Equal("ork", snapshot["target"].GetProperty("name").GetString());
    }
}
