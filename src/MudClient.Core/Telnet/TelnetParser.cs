namespace MudClient.Core.Telnet;

/// <summary>
/// Stateful Telnet parser. One Telnet command can be split across any number of TCP reads,
/// so the parser deliberately keeps its state between calls to <see cref="Feed"/>.
/// </summary>
public sealed class TelnetParser
{
    private enum ParserState
    {
        Data,
        Iac,
        NegotiationOption,
        SubnegotiationOption,
        SubnegotiationData,
        SubnegotiationIac,
    }

    private readonly List<byte> _dataBuffer = [];
    private readonly List<byte> _subnegotiationBuffer = [];
    private ParserState _state = ParserState.Data;
    private byte _negotiationCommand;
    private byte _subnegotiationOption;

    public IReadOnlyList<TelnetToken> Feed(ReadOnlySpan<byte> input)
    {
        var tokens = new List<TelnetToken>();

        foreach (var value in input)
        {
            switch (_state)
            {
                case ParserState.Data:
                    if (value == TelnetConstants.Iac)
                    {
                        _state = ParserState.Iac;
                    }
                    else
                    {
                        _dataBuffer.Add(value);
                    }

                    break;

                case ParserState.Iac:
                    HandleIac(value, tokens);
                    break;

                case ParserState.NegotiationOption:
                    tokens.Add(new TelnetNegotiationToken(_negotiationCommand, value));
                    _state = ParserState.Data;
                    break;

                case ParserState.SubnegotiationOption:
                    _subnegotiationOption = value;
                    _subnegotiationBuffer.Clear();
                    _state = ParserState.SubnegotiationData;
                    break;

                case ParserState.SubnegotiationData:
                    if (value == TelnetConstants.Iac)
                    {
                        _state = ParserState.SubnegotiationIac;
                    }
                    else
                    {
                        _subnegotiationBuffer.Add(value);
                    }

                    break;

                case ParserState.SubnegotiationIac:
                    if (value == TelnetConstants.Iac)
                    {
                        _subnegotiationBuffer.Add(TelnetConstants.Iac);
                        _state = ParserState.SubnegotiationData;
                    }
                    else if (value == TelnetConstants.Se)
                    {
                        tokens.Add(new TelnetSubnegotiationToken(
                            _subnegotiationOption,
                            [.. _subnegotiationBuffer]));
                        _subnegotiationBuffer.Clear();
                        _state = ParserState.Data;
                    }
                    else
                    {
                        // Malformed sequence. Preserve the bytes instead of silently deleting them.
                        _subnegotiationBuffer.Add(TelnetConstants.Iac);
                        _subnegotiationBuffer.Add(value);
                        _state = ParserState.SubnegotiationData;
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unknown parser state: {_state}");
            }
        }

        FlushData(tokens);
        return tokens;
    }

    private void HandleIac(byte value, List<TelnetToken> tokens)
    {
        switch (value)
        {
            case TelnetConstants.Iac:
                _dataBuffer.Add(TelnetConstants.Iac);
                _state = ParserState.Data;
                break;

            case TelnetConstants.Will:
            case TelnetConstants.Wont:
            case TelnetConstants.Do:
            case TelnetConstants.Dont:
                FlushData(tokens);
                _negotiationCommand = value;
                _state = ParserState.NegotiationOption;
                break;

            case TelnetConstants.Sb:
                FlushData(tokens);
                _state = ParserState.SubnegotiationOption;
                break;

            default:
                FlushData(tokens);
                tokens.Add(new TelnetCommandToken(value));
                _state = ParserState.Data;
                break;
        }
    }

    private void FlushData(List<TelnetToken> tokens)
    {
        if (_dataBuffer.Count == 0)
        {
            return;
        }

        tokens.Add(new TelnetDataToken([.. _dataBuffer]));
        _dataBuffer.Clear();
    }
}
