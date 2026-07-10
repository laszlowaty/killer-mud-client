namespace MudClient.App.Controls;

internal abstract record AnsiToken;

internal sealed record AnsiTextToken(string Text, AnsiStyle Style) : AnsiToken;

internal sealed record AnsiNewLineToken : AnsiToken;

internal sealed record AnsiCarriageReturnToken : AnsiToken;
