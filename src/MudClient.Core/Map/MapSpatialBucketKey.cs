namespace MudClient.Core.Map;

public readonly record struct MapSpatialBucketKey(int AreaId, double Z, long BucketX, long BucketY);
