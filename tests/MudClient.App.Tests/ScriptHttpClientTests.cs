using MudClient.App.Services;
using MudClient.Core.Scripting;

namespace MudClient.App.Tests;

public sealed class ScriptHttpClientTests
{
    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/file")]
    [InlineData("not-an-address")]
    public async Task SendAsync_RejectsNonHttpAddresses(string url)
    {
        var request = new ScriptHttpRequest(
            "GET",
            url,
            new Dictionary<string, string>(),
            null,
            1000);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => new ScriptHttpClient().SendAsync(request, CancellationToken.None));

        Assert.Contains("HTTP", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://[::1]/")]
    public async Task SendAsync_RejectsLocalAddresses(string url)
    {
        var request = new ScriptHttpRequest(
            "GET",
            url,
            new Dictionary<string, string>(),
            null,
            1000);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => new ScriptHttpClient().SendAsync(request, CancellationToken.None));

        Assert.Contains("lokalnym", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_RejectsUnsupportedMethodBeforeNetworkAccess()
    {
        var request = new ScriptHttpRequest(
            "CONNECT",
            "https://example.com/",
            new Dictionary<string, string>(),
            null,
            1000);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => new ScriptHttpClient().SendAsync(request, CancellationToken.None));

        Assert.Contains("metoda", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
