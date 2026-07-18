using System.Net.Http.Json;
using System.Reflection;
using MudClient.App.Models;

namespace MudClient.App.Services;

public interface IUpdateCheckService
{
    Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}

internal sealed class UpdateCheckService : IUpdateCheckService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    internal static readonly Uri DefaultVersionManifestUri = new(
        "https://laszlowaty.github.io/killer-mud-client/app-version.json");
    private static readonly Uri ReleasesUri = new(
        "https://github.com/laszlowaty/killer-mud-client/releases/");
    private static readonly Uri ChangelogUri = new(
        "https://laszlowaty.github.io/killer-mud-client/changelog.html");

    private readonly HttpClient _httpClient;
    private readonly Uri _versionManifestUri;
    private readonly ReleaseVersion? _currentVersion;

    public UpdateCheckService(
        HttpClient? httpClient = null,
        Uri? versionManifestUri = null,
        string? currentVersion = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
        _versionManifestUri = versionManifestUri ?? DefaultVersionManifestUri;
        _currentVersion = ReleaseVersion.Parse(currentVersion ?? GetCurrentVersion());
    }

    public async Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        var manifest = await _httpClient.GetFromJsonAsync<AppVersionManifest>(
            _versionManifestUri,
            timeoutCts.Token);

        if (manifest is null || manifest.SchemaVersion != 1 || _currentVersion is null)
        {
            return null;
        }

        var availableVersion = ReleaseVersion.Parse(manifest.Version);
        if (availableVersion is null || availableVersion.CompareTo(_currentVersion) <= 0)
        {
            return null;
        }

        return new AvailableUpdate(
            availableVersion.Display,
            manifest.Prerelease,
            new Uri(ReleasesUri, $"tag/v{availableVersion.Display}"),
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
        return client;
    }

    private sealed class AppVersionManifest
    {
        public int SchemaVersion { get; init; }

        public string Version { get; init; } = string.Empty;

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
