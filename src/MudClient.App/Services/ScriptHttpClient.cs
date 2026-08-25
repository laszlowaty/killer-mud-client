using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using MudClient.Core.Scripting;

namespace MudClient.App.Services;

/// <summary>
/// Bounded HTTP/HTTPS transport for user scripts. Requests cannot reach the
/// loopback interface, private/link-local networks, proxies, or non-HTTP schemes.
/// Redirect targets are validated independently.
/// </summary>
public sealed class ScriptHttpClient : IScriptHttpClient
{
    public const int DefaultTimeoutMilliseconds = 10_000;
    public const int MaximumTimeoutMilliseconds = 30_000;
    public const int MaximumRequestBodyBytes = 256 * 1024;
    public const int MaximumResponseBodyBytes = 1024 * 1024;
    public const int MaximumHeaders = 32;
    public const int MaximumRequestHeaderBytes = 32 * 1024;
    public const int MaximumRedirects = 5;

    private static readonly HashSet<string> AllowedMethods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD",
        };

    private static readonly HashSet<string> ForbiddenHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Connection",
            "Content-Length",
            "Host",
            "Proxy-Authorization",
            "Proxy-Connection",
            "TE",
            "Trailer",
            "Transfer-Encoding",
            "Upgrade",
        };

    private static readonly HttpClient Client = CreateClient();
    private static ReadOnlySpan<byte> WellKnownNat64Prefix =>
        [0x00, 0x64, 0xff, 0x9b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    public async Task<ScriptHttpResponse> SendAsync(
        ScriptHttpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var method = NormalizeMethod(request.Method);
        var uri = await ValidateUriAsync(request.Url, cancellationToken).ConfigureAwait(false);
        var headers = ValidateHeaders(request.Headers);
        var body = request.Body;
        ValidateBody(body);
        if (body is not null && method is "GET" or "HEAD")
        {
            throw new ArgumentException($"Metoda {method} nie może zawierać treści requestu.");
        }
        var timeout = Math.Clamp(
            request.TimeoutMilliseconds <= 0
                ? DefaultTimeoutMilliseconds
                : request.TimeoutMilliseconds,
            1,
            MaximumTimeoutMilliseconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        for (var redirect = 0; ; redirect++)
        {
            using var message = CreateRequest(method, uri, headers, body);
            using var response = await Client.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token)
                .ConfigureAwait(false);

            if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
            {
                if (redirect >= MaximumRedirects)
                {
                    throw new HttpRequestException(
                        $"Przekroczono limit {MaximumRedirects} przekierowań HTTP.");
                }

                var nextUri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                nextUri = await ValidateUriAsync(nextUri.AbsoluteUri, timeoutCts.Token)
                    .ConfigureAwait(false);

                if (!HasSameOrigin(uri, nextUri))
                {
                    headers.Remove("Authorization");
                }

                if (response.StatusCode == HttpStatusCode.SeeOther
                    || ((response.StatusCode == HttpStatusCode.Moved
                         || response.StatusCode == HttpStatusCode.Redirect)
                        && !string.Equals(method, "GET", StringComparison.Ordinal)
                        && !string.Equals(method, "HEAD", StringComparison.Ordinal)))
                {
                    method = "GET";
                    body = null;
                    headers.Remove("Content-Type");
                }

                uri = nextUri;
                continue;
            }

            var responseBody = await ReadBoundedBodyAsync(response.Content, timeoutCts.Token)
                .ConfigureAwait(false);
            return new ScriptHttpResponse(
                (int) response.StatusCode,
                response.ReasonPhrase ?? string.Empty,
                uri.AbsoluteUri,
                CollectHeaders(response),
                responseBody);
        }
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = ConnectToPublicAddressAsync,
            MaxResponseHeadersLength = 32,
            UseCookies = false,
            UseProxy = false,
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(
                context.DnsEndPoint.Host,
                cancellationToken)
            .ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new HttpRequestException(
                "Skrypty HTTP nie mogą łączyć się z adresem lokalnym ani prywatnym.");
        }

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                await socket.ConnectAsync(
                        new IPEndPoint(address, context.DnsEndPoint.Port),
                        cancellationToken)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                lastError = exception;
                socket.Dispose();
            }
        }

        throw new HttpRequestException("Nie udało się połączyć z serwerem HTTP.", lastError);
    }

    private static async Task<Uri> ValidateUriAsync(
        string url,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Adres musi być pełnym adresem HTTP lub HTTPS bez danych logowania.");
        }

        var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken)
            .ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new ArgumentException(
                "Skrypty HTTP nie mogą łączyć się z adresem lokalnym ani prywatnym.");
        }

        return uri;
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.AsSpan(0, 12).SequenceEqual(WellKnownNat64Prefix))
            {
                return IsPublicAddress(new IPAddress(bytes.AsSpan(12, 4)));
            }

            return !IPAddress.IsLoopback(address)
                   && !address.IsIPv6LinkLocal
                   && !address.IsIPv6SiteLocal
                   && !address.IsIPv6Multicast
                   && (bytes[0] & 0xe0) == 0x20;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var value = address.GetAddressBytes();
        return value[0] switch
        {
            0 or 10 or 127 => false,
            100 when value[1] is >= 64 and <= 127 => false,
            169 when value[1] == 254 => false,
            172 when value[1] is >= 16 and <= 31 => false,
            192 when value[1] == 168 => false,
            192 when value[1] == 0 && value[2] is 0 or 2 => false,
            192 when value[1] == 88 && value[2] == 99 => false,
            198 when value[1] is 18 or 19 => false,
            198 when value[1] == 51 && value[2] == 100 => false,
            203 when value[1] == 0 && value[2] == 113 => false,
            >= 224 => false,
            _ => true,
        };
    }

    private static string NormalizeMethod(string method)
    {
        var normalized = string.IsNullOrWhiteSpace(method)
            ? "GET"
            : method.Trim().ToUpperInvariant();
        return AllowedMethods.Contains(normalized)
            ? normalized
            : throw new ArgumentException($"Niedozwolona metoda HTTP: {normalized}.");
    }

    private static Dictionary<string, string> ValidateHeaders(
        IReadOnlyDictionary<string, string> source)
    {
        if (source.Count > MaximumHeaders)
        {
            throw new ArgumentException($"Request może mieć najwyżej {MaximumHeaders} nagłówki.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var totalBytes = 0;
        foreach (var (name, value) in source)
        {
            if (string.IsNullOrWhiteSpace(name)
                || ForbiddenHeaders.Contains(name)
                || name.Any(character => character <= 32 || character >= 127 || character == ':')
                || value.Contains('\r')
                || value.Contains('\n'))
            {
                throw new ArgumentException($"Niedozwolony nagłówek HTTP: {name}.");
            }

            totalBytes += Encoding.UTF8.GetByteCount(name) + Encoding.UTF8.GetByteCount(value);
            if (totalBytes > MaximumRequestHeaderBytes)
            {
                throw new ArgumentException(
                    $"Nagłówki requestu mogą mieć łącznie najwyżej {MaximumRequestHeaderBytes} bajtów UTF-8.");
            }

            headers[name] = value;
        }

        return headers;
    }

    private static void ValidateBody(string? body)
    {
        if (body is not null && Encoding.UTF8.GetByteCount(body) > MaximumRequestBodyBytes)
        {
            throw new ArgumentException(
                $"Treść requestu może mieć najwyżej {MaximumRequestBodyBytes} bajtów UTF-8.");
        }
    }

    private static HttpRequestMessage CreateRequest(
        string method,
        Uri uri,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var message = new HttpRequestMessage(new HttpMethod(method), uri);
        if (body is not null)
        {
            message.Content = new StringContent(body, Encoding.UTF8);
        }

        foreach (var (name, value) in headers)
        {
            if (message.Content is not null
                && name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                message.Content.Headers.Remove(name);
                _ = message.Content.Headers.TryAddWithoutValidation(name, value);
            }
            else if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                throw new ArgumentException($"Nieprawidłowy nagłówek HTTP: {name}.");
            }
        }

        return message;
    }

    private static async Task<string> ReadBoundedBodyAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBodyBytes)
        {
            throw new HttpRequestException(
                $"Odpowiedź HTTP przekracza limit {MaximumResponseBodyBytes} bajtów.");
        }

        await using var input = await content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > MaximumResponseBodyBytes)
            {
                throw new HttpRequestException(
                    $"Odpowiedź HTTP przekracza limit {MaximumResponseBodyBytes} bajtów.");
            }

            output.Write(buffer, 0, read);
        }

        return GetResponseEncoding(content.Headers.ContentType).GetString(output.ToArray());
    }

    private static Encoding GetResponseEncoding(MediaTypeHeaderValue? contentType)
    {
        var charset = contentType?.CharSet?.Trim('"');
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key.ToLowerInvariant(),
                group => string.Join(", ", group.SelectMany(header => header.Value)),
                StringComparer.OrdinalIgnoreCase);
        return headers;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.Moved
        or HttpStatusCode.Redirect
        or HttpStatusCode.RedirectMethod
        or HttpStatusCode.TemporaryRedirect
        or HttpStatusCode.PermanentRedirect;

    private static bool HasSameOrigin(Uri first, Uri second) =>
        string.Equals(first.Scheme, second.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(first.Host, second.Host, StringComparison.OrdinalIgnoreCase)
        && first.Port == second.Port;
}
