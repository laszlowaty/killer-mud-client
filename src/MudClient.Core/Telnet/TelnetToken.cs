namespace MudClient.Core.Telnet;

public abstract record TelnetToken;

public sealed record TelnetDataToken(byte[] Data) : TelnetToken;

public sealed record TelnetNegotiationToken(byte Command, byte Option) : TelnetToken;

public sealed record TelnetSubnegotiationToken(byte Option, byte[] Data) : TelnetToken;

public sealed record TelnetCommandToken(byte Command) : TelnetToken;
