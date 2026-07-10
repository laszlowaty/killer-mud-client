using System.Buffers;

namespace MudClient.Core.Telnet;

public static class TelnetWriter
{
    public static byte[] Negotiation(byte command, byte option) =>
        [TelnetConstants.Iac, command, option];

    public static byte[] Subnegotiation(byte option, ReadOnlySpan<byte> payload)
    {
        var writer = new ArrayBufferWriter<byte>(payload.Length + 5);
        WriteByte(writer, TelnetConstants.Iac);
        WriteByte(writer, TelnetConstants.Sb);
        WriteByte(writer, option);

        foreach (var value in payload)
        {
            WriteByte(writer, value);
            if (value == TelnetConstants.Iac)
            {
                WriteByte(writer, TelnetConstants.Iac);
            }
        }

        WriteByte(writer, TelnetConstants.Iac);
        WriteByte(writer, TelnetConstants.Se);
        return writer.WrittenSpan.ToArray();
    }

    public static byte[] EscapeData(ReadOnlySpan<byte> payload)
    {
        var extraBytes = 0;
        foreach (var value in payload)
        {
            if (value == TelnetConstants.Iac)
            {
                extraBytes++;
            }
        }

        if (extraBytes == 0)
        {
            return payload.ToArray();
        }

        var result = new byte[payload.Length + extraBytes];
        var destinationIndex = 0;

        foreach (var value in payload)
        {
            result[destinationIndex++] = value;
            if (value == TelnetConstants.Iac)
            {
                result[destinationIndex++] = TelnetConstants.Iac;
            }
        }

        return result;
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte value)
    {
        var span = writer.GetSpan(1);
        span[0] = value;
        writer.Advance(1);
    }
}
