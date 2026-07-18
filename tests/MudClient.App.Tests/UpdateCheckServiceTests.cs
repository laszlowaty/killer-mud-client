using System.Net;
using System.Text;
using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_NewerPrerelease_ReturnsReleaseAndLinks()
    {
        using var httpClient = CreateClient("""
            {
              "schemaVersion": 1,
              "version": "999.0.0-beta.2",
              "prerelease": true
            }
            """);
        var service = new UpdateCheckService(httpClient, currentVersion: "0.5.9");

        var update = await service.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal("999.0.0-beta.2", update.Version);
        Assert.True(update.IsPrerelease);
        Assert.Equal(
            "https://github.com/laszlowaty/killer-mud-client/releases/tag/v999.0.0-beta.2",
            update.ReleasePageUri.AbsoluteUri.TrimEnd('/'));
        Assert.Equal(
            "https://laszlowaty.github.io/killer-mud-client/changelog.html",
            update.ChangelogUri.AbsoluteUri);
    }

    [Fact]
    public async Task CheckForUpdateAsync_UsesGitHubPagesManifestInsteadOfGitHubApi()
    {
        var handler = new StubHandler("""
            { "schemaVersion": 1, "version": "999.0.0-beta.10", "prerelease": true }
            """);
        using var httpClient = new HttpClient(handler);
        var service = new UpdateCheckService(httpClient, currentVersion: "0.5.9");

        var update = await service.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal("999.0.0-beta.10", update.Version);
        Assert.Equal(UpdateCheckService.DefaultVersionManifestUri, handler.RequestUri);
    }

    [Fact]
    public async Task CheckForUpdateAsync_OlderRelease_ReturnsNull()
    {
        using var httpClient = CreateClient("""
            { "schemaVersion": 1, "version": "0.5.8", "prerelease": false }
            """);
        var service = new UpdateCheckService(httpClient, currentVersion: "0.5.9");

        var update = await service.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.Null(update);
    }

    private static HttpClient CreateClient(string response) => new(new StubHandler(response));

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            });
        }
    }
}
