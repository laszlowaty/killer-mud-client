using System.IO.Compression;
using System.Text.Json;
using MudClient.Core.Map;

namespace MudClient.App.Services;

internal sealed class MapEditorRecoveryStore : IDisposable, IAsyncDisposable
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumUndoEntries = 5;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _sync = new();
    private CancellationTokenSource? _scheduledCancellation;
    private MapEditorRecoveryState? _latest;
    private bool _disposed;

    public MapEditorRecoveryStore(string editorDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorDirectory);
        _path = System.IO.Path.Combine(editorDirectory, "recovery.json.gz");
    }

    public string Path => _path;

    public bool Exists => File.Exists(_path);

    public async Task<MapEditorRecoveryState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            await using var file = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                useAsync: true);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress, leaveOpen: false);
            var state = await JsonSerializer.DeserializeAsync<MapEditorRecoveryState>(
                gzip,
                SerializerOptions,
                cancellationToken);
            if (state is null ||
                state.SchemaVersion != CurrentSchemaVersion ||
                state.Current is null ||
                !IsValidDocument(state.Current))
            {
                return null;
            }

            state.UndoHistory = (state.UndoHistory ?? [])
                .Where(IsValidDocument)
                .TakeLast(MaximumUndoEntries)
                .ToArray();
            return state;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException)
        {
            // Recovery is optional. A corrupt checkpoint must not hide the normal working/base map.
            System.Diagnostics.Trace.WriteLine(exception);
            return null;
        }
    }

    public void Schedule(
        MapDocument current,
        IReadOnlyList<MapDocument> undoHistory,
        bool isDirty,
        string baselineIdentity)
    {
        var state = CreateState(current, undoHistory, isDirty, baselineIdentity);
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _latest = state;
            _scheduledCancellation?.Cancel();
            _scheduledCancellation?.Dispose();
            _scheduledCancellation = new CancellationTokenSource();
            _ = SaveAfterDelayAsync(state, _scheduledCancellation.Token);
        }
    }

    public async Task SaveCheckpointAsync(
        MapDocument current,
        IReadOnlyList<MapDocument> undoHistory,
        bool isDirty,
        string baselineIdentity,
        CancellationToken cancellationToken = default)
    {
        var state = CreateState(current, undoHistory, isDirty, baselineIdentity);
        CancelScheduledWrite();
        lock (_sync)
        {
            _latest = state;
        }

        await SaveCoreAsync(state, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        CancelScheduledWrite();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            lock (_sync)
            {
                _latest = null;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        MapEditorRecoveryState? latest;
        lock (_sync)
        {
            latest = _latest;
        }

        CancelScheduledWrite();
        if (latest is not null)
        {
            await SaveCoreAsync(latest, cancellationToken).ConfigureAwait(false);
        }
    }

    private static MapEditorRecoveryState CreateState(
        MapDocument current,
        IReadOnlyList<MapDocument> undoHistory,
        bool isDirty,
        string baselineIdentity) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        SavedAtUtc = DateTimeOffset.UtcNow,
        IsDirty = isDirty,
        BaselineIdentity = baselineIdentity,
        Current = current,
        UndoHistory = undoHistory.TakeLast(MaximumUndoEntries).ToArray(),
    };

    private static bool IsValidDocument(MapDocument document) =>
        document.Areas is not null &&
        document.Areas.All(area =>
            area.Rooms is not null &&
            area.Labels is not null &&
            area.Rooms.All(room =>
                room.Exits is not null &&
                double.IsFinite(room.Coordinates.X) &&
                double.IsFinite(room.Coordinates.Y) &&
                double.IsFinite(room.Coordinates.Z)) &&
            area.Labels.All(label =>
                double.IsFinite(label.Coordinates.X) &&
                double.IsFinite(label.Coordinates.Y) &&
                double.IsFinite(label.Coordinates.Z)));

    private async Task SaveAfterDelayAsync(MapEditorRecoveryState state, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
            await SaveCoreAsync(state, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer edit superseded this checkpoint or the store is shutting down.
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            // Autosave is best-effort; explicit save still reports its own errors to the user.
            System.Diagnostics.Trace.WriteLine(exception);
        }
    }

    private async Task SaveCoreAsync(MapEditorRecoveryState state, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            await using (var file = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             useAsync: true))
            await using (var gzip = new GZipStream(file, CompressionLevel.Fastest, leaveOpen: false))
            {
                await JsonSerializer.SerializeAsync(gzip, state, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _writeLock.Release();
        }
    }

    private void CancelScheduledWrite()
    {
        lock (_sync)
        {
            _scheduledCancellation?.Cancel();
            _scheduledCancellation?.Dispose();
            _scheduledCancellation = null;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        CancelScheduledWrite();
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
        }

        await FlushAsync().ConfigureAwait(false);
        Dispose();
        _writeLock.Dispose();
    }
}

internal sealed class MapEditorRecoveryState
{
    public int SchemaVersion { get; set; }

    public DateTimeOffset SavedAtUtc { get; set; }

    public bool IsDirty { get; set; }

    public string BaselineIdentity { get; set; } = string.Empty;

    public MapDocument? Current { get; set; }

    public IReadOnlyList<MapDocument> UndoHistory { get; set; } = [];
}
