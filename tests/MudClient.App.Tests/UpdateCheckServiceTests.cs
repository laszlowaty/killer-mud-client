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
            [
              {
                "tag_name": "v999.0.0-beta.2",
                "html_url": "https://github.com/laszlowaty/killer-mud-client/releases/tag/v999.0.0-beta.2",
                "draft": false,
                "prerelease": true
              }
            ]
            """);
        var service = new UpdateCheckService(httpClient);

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
    public async Task CheckForUpdateAsync_SelectsHighestSemanticVersionAndSkipsDrafts()
    {
        using var httpClient = CreateClient("""
            [
              {
                "tag_name": "v1000.0.0",
                "html_url": "https://example.test/draft",
                "draft": true,
                "prerelease": false
              },
              {
                "tag_name": "v999.0.0-beta.2",
                "html_url": "https://example.test/beta-2",
                "draft": false,
                "prerelease": true
              },
              {
                "tag_name": "v999.0.0-beta.10",
                "html_url": "https://example.test/beta-10",
                "draft": false,
                "prerelease": true
              }
            ]
            """);
        var service = new UpdateCheckService(httpClient);

        var update = await service.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal("999.0.0-beta.10", update.Version);
        Assert.Equal("https://example.test/beta-10", update.ReleasePageUri.AbsoluteUri.TrimEnd('/'));
    }

    [Fact]
    public async Task CheckForUpdateAsync_OlderRelease_ReturnsNull()
    {
        using var httpClient = CreateClient("""
            [
              {
                "tag_name": "v0.0.1",
                "html_url": "https://example.test/old",
                "draft": false,
                "prerelease": false
              }
            ]
            """);
        var service = new UpdateCheckService(httpClient);

        var update = await service.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.Null(update);
    }

    private static HttpClient CreateClient(string response) => new(new StubHandler(response));

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            });
    }
}
