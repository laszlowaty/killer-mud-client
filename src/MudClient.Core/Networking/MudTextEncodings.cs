using System.Text;

namespace MudClient.Core.Networking;

/// <summary>
/// Text encodings selectable per profile for talking to the MUD server.
/// Older Polish MUDs commonly use ISO-8859-2 or Windows-1250 instead of UTF-8.
/// </summary>
public static class MudTextEncodings
{
    /// <summary>Detect the server's encoding from the first non-ASCII bytes it sends (see <see cref="MudSession"/>).</summary>
    public const string Auto = "Auto";

    public const string Utf8 = "UTF-8";
    public const string Iso88592 = "ISO-8859-2";
    public const string Windows1250 = "Windows-1250";

    public static readonly IReadOnlyList<string> All = [Auto, Utf8, Iso88592, Windows1250];

    /// <summary>Legacy fallback used by auto-detection once server bytes turn out not to be valid UTF-8.</summary>
    internal const string AutoFallback = Windows1250;

    static MudTextEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Resolves a stored encoding name to a usable <see cref="Encoding"/>, defaulting to UTF-8.</summary>
    public static Encoding Resolve(string? name) => name switch
    {
        Iso88592 => Encoding.GetEncoding("iso-8859-2"),
        Windows1250 => Encoding.GetEncoding(1250),
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
    };

    /// <summary>
    /// Checks whether <paramref name="data"/> is valid UTF-8 so far, used to auto-detect the server's
    /// encoding from its first non-ASCII bytes. A throwing decoder only faults on genuinely malformed
    /// sequences — an incomplete trailing multi-byte sequence at a chunk boundary is not an error, so
    /// this is safe to call per-chunk without any risk of a false negative from split reads.
    /// </summary>
    internal static bool LooksLikeUtf8(ReadOnlySpan<byte> data)
    {
        var probeDecoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetDecoder();
        var chars = new char[data.Length];

        try
        {
            probeDecoder.Convert(data, chars, flush: false, out _, out _, out _);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
