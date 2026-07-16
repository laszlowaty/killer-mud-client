using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using MudClient.App.Models;

namespace MudClient.App.Services;

public interface IUpdateCheckService
{
    Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}

internal sealed class UpdateCheckService(HttpClient? httpClient = null) : IUpdateCheckService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private static readonly Uri ReleasesApiUri = new(
        "https://api.github.com/repos/laszlowaty/killer-mud-client/releases?per_page=20");
    private static readonly Uri ChangelogUri = new(
        "https://laszlowaty.github.io/killer-mud-client/changelog.html");

    private readonly HttpClient _httpClient = httpClient ?? SharedHttpClient;

    public async Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        var releases = await _httpClient.GetFromJsonAsync<GitHubRelease[]>(
            ReleasesApiUri,
            timeoutCts.Token);

        var currentVersion = ReleaseVersion.Parse(GetCurrentVersion());
        if (currentVersion is null || releases is null)
        {
            return null;
        }

        var newest = releases
            .Where(release => !release.Draft && release.HtmlUrl is not null)
            .Select(release => (Release: release, Version: ReleaseVersion.Parse(release.TagName)))
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefault();

        if (newest.Version is null || newest.Version.CompareTo(currentVersion) <= 0)
        {
            return null;
        }

        return new AvailableUpdate(
            newest.Version.Display,
            newest.Release.Prerelease,
            newest.Release.HtmlUrl!,
            ChangelogUri);
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateCheckService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KillerMudClient-UpdateCheck");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("html_url")]
        public Uri? HtmlUrl { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }
    }

    private sealed class ReleaseVersion : IComparable<ReleaseVersion>
    {
        private ReleaseVersion(Version core, string? prerelease, string display)
        {
            Core = core;
            Prerelease = prerelease;
            Display = display;
        }

        private Version Core { get; }
        private string? Prerelease { get; }
        public string Display { get; }

        public static ReleaseVersion? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().TrimStart('v', 'V');
            var metadataIndex = normalized.IndexOf('+');
            if (metadataIndex >= 0)
            {
                normalized = normalized[..metadataIndex];
            }

            var prereleaseIndex = normalized.IndexOf('-');
            var coreText = prereleaseIndex >= 0 ? normalized[..prereleaseIndex] : normalized;
            var prerelease = prereleaseIndex >= 0 ? normalized[(prereleaseIndex + 1)..] : null;
            if (!Version.TryParse(coreText, out var core))
            {
                return null;
            }

            return new ReleaseVersion(core, prerelease, normalized);
        }

        public int CompareTo(ReleaseVersion? other)
        {
            if (other is null)
            {
                return 1;
            }

            var coreComparison = Core.CompareTo(other.Core);
            if (coreComparison != 0)
            {
                return coreComparison;
            }

            if (Prerelease is null)
            {
                return other.Prerelease is null ? 0 : 1;
            }

            if (other.Prerelease is null)
            {
                return -1;
            }

            return ComparePrerelease(Prerelease, other.Prerelease);
        }

        private static int ComparePrerelease(string left, string right)
        {
            var leftParts = left.Split('.');
            var rightParts = right.Split('.');
            for (var index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++)
            {
                if (index >= leftParts.Length)
                {
                    return -1;
                }

                if (index >= rightParts.Length)
                {
                    return 1;
                }

                var leftNumeric = int.TryParse(leftParts[index], out var leftNumber);
                var rightNumeric = int.TryParse(rightParts[index], out var rightNumber);
                int comparison;
                if (leftNumeric && rightNumeric)
                {
                    comparison = leftNumber.CompareTo(rightNumber);
                }
                else if (leftNumeric != rightNumeric)
                {
                    comparison = leftNumeric ? -1 : 1;
                }
                else
                {
                    comparison = string.Compare(leftParts[index], rightParts[index], StringComparison.OrdinalIgnoreCase);
                }

                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        }
    }
}
