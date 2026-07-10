namespace MudClient.Core.Telnet;

public static class TelnetConstants
{
    public const byte Binary = 0;
    public const byte Echo = 1;
    public const byte SuppressGoAhead = 3;
    public const byte TerminalType = 24;
    public const byte EndOfRecord = 25;
    public const byte Naws = 31;
    public const byte Mccp2 = 86;
    public const byte Gmcp = 201;

    public const byte Se = 240;
    public const byte Nop = 241;
    public const byte DataMark = 242;
    public const byte Break = 243;
    public const byte InterruptProcess = 244;
    public const byte AbortOutput = 245;
    public const byte AreYouThere = 246;
    public const byte EraseCharacter = 247;
    public const byte EraseLine = 248;
    public const byte GoAhead = 249;
    public const byte Sb = 250;
    public const byte Will = 251;
    public const byte Wont = 252;
    public const byte Do = 253;
    public const byte Dont = 254;
    public const byte Iac = 255;

    public const byte TerminalTypeIs = 0;
    public const byte TerminalTypeSend = 1;
}
