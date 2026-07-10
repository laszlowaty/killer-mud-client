using System.Text;

namespace MudClient.Core.Gmcp;

public sealed record GmcpMessage(string Package, string Json)
{
    public static GmcpMessage Parse(ReadOnlySpan<byte> payload)
    {
        var text = Encoding.UTF8.GetString(payload).Trim();
        if (text.Length == 0)
        {
            return new GmcpMessage(string.Empty, string.Empty);
        }

        var separator = text.IndexOf(' ');
        if (separator < 0)
        {
            return new GmcpMessage(text, string.Empty);
        }

        return new GmcpMessage(
            text[..separator],
            text[(separator + 1)..].TrimStart());
    }
}
