namespace MudClient.Core.Networking;

/// <summary>
/// Read-only stream that first serves a fixed prefix and then reads from the inner stream.
/// Used by MCCP2: the compressed zlib stream starts mid-buffer (right after IAC SB 86 IAC SE),
/// so the leftover bytes of the current TCP read must be prepended to further network reads.
/// </summary>
internal sealed class PrefixedReadStream : Stream
{
    private readonly Stream _inner;
    private byte[] _prefix;
    private int _prefixOffset;

    public PrefixedReadStream(byte[] prefix, Stream inner)
    {
        _prefix = prefix;
        _inner = inner;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var fromPrefix = ReadFromPrefix(buffer);
        return fromPrefix > 0 ? fromPrefix : _inner.Read(buffer);
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var fromPrefix = ReadFromPrefix(buffer.Span);
        return fromPrefix > 0
            ? fromPrefix
            : await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    private int ReadFromPrefix(Span<byte> buffer)
    {
        var remaining = _prefix.Length - _prefixOffset;
        if (remaining == 0)
        {
            return 0;
        }

        var count = Math.Min(remaining, buffer.Length);
        _prefix.AsSpan(_prefixOffset, count).CopyTo(buffer);
        _prefixOffset += count;

        if (_prefixOffset == _prefix.Length)
        {
            _prefix = [];
            _prefixOffset = 0;
        }

        return count;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    // The inner NetworkStream lifetime is owned by MudSession; do not dispose it here.
}
