using System.Text.Json;
using System.Text.Json.Serialization;

namespace MudClient.Core.Map;

public sealed class MapSettingsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<MapSettings> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return MapSettings.CreateDefault();
        }

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var raw = await JsonSerializer
                .DeserializeAsync<RawMapSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (raw is null)
            {
                return MapSettings.CreateDefault();
            }

            var defaults = MapSettings.CreateDefault();

            return new MapSettings
            {
                WorldFile = raw.WorldFile ?? defaults.WorldFile,
                SectorDirectory = raw.SectorDirectory ?? defaults.SectorDirectory,
                SectorManifest = raw.SectorManifest ?? defaults.SectorManifest,
                PixelsPerCoordinateUnit = raw.PixelsPerCoordinateUnit ?? defaults.PixelsPerCoordinateUnit,
                RoomSize = raw.RoomSize ?? defaults.RoomSize,
                MinimumZoom = raw.MinimumZoom ?? defaults.MinimumZoom,
                MaximumZoom = raw.MaximumZoom ?? defaults.MaximumZoom,
                SpatialBucketSize = raw.SpatialBucketSize ?? defaults.SpatialBucketSize,
                GmcpLocation = raw.GmcpLocation is null
                    ? defaults.GmcpLocation
                    : new MapGmcpLocationSettings
                    {
                        Packages = raw.GmcpLocation.Packages ?? defaults.GmcpLocation.Packages,
                        VnumPaths = raw.GmcpLocation.VnumPaths ?? defaults.GmcpLocation.VnumPaths,
                    },
            };
        }
        catch (JsonException)
        {
            return MapSettings.CreateDefault();
        }
    }

    private sealed class RawMapSettings
    {
        public string? WorldFile { get; set; }

        public string? SectorDirectory { get; set; }

        public string? SectorManifest { get; set; }

        public double? PixelsPerCoordinateUnit { get; set; }

        public double? RoomSize { get; set; }

        public double? MinimumZoom { get; set; }

        public double? MaximumZoom { get; set; }

        public int? SpatialBucketSize { get; set; }

        [JsonPropertyName("gmcpLocation")]
        public RawGmcpLocation? GmcpLocation { get; set; }
    }

    private sealed class RawGmcpLocation
    {
        public List<string>? Packages { get; set; }

        public List<string>? VnumPaths { get; set; }
    }
}
