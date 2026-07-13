using Avalonia.Media;
using MudClient.App.Controls;

namespace MudClient.App.Tests;

public sealed class AnsiStreamParserTests
{
    private const char Esc = '';

    private static string PlainText(IReadOnlyList<AnsiToken> tokens) =>
        string.Concat(tokens.OfType<AnsiTextToken>().Select(t => t.Text));

    [Fact]
    public void PlainTextPassesThroughUnchanged()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed("(gizarma 88%)");

        var token = Assert.Single(tokens);
        var text = Assert.IsType<AnsiTextToken>(token);
        Assert.Equal("(gizarma 88%)", text.Text);
    }

    [Fact]
    public void SgrColorAppliesToFollowingText()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"{Esc}[32mhello{Esc}[0m");

        var text = Assert.IsType<AnsiTextToken>(Assert.Single(tokens));
        Assert.Equal("hello", text.Text);
        Assert.Equal(Color.FromRgb(13, 188, 121), text.Style.Foreground);
    }

    [Theory]
    [InlineData(AnsiColorPalette.Colorblind, 136, 136, 136)]
    [InlineData(AnsiColorPalette.Vivid, 0, 128, 0)]
    public void NamedSchemeChangesStandardAnsiColors(
        string scheme, byte red, byte green, byte blue)
    {
        var parser = new AnsiStreamParser(scheme);

        var token = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[32mx")));

        Assert.Equal(Color.FromRgb(red, green, blue), token.Style.Foreground);
    }

    [Theory]
    [InlineData(31, 128, 0, 0)]
    [InlineData(33, 128, 128, 0)]
    [InlineData(91, 255, 0, 0)]
    [InlineData(93, 255, 255, 0)]
    public void VividSchemeUsesMudletRedAndYellow(
        int sgrCode, byte red, byte green, byte blue)
    {
        var parser = new AnsiStreamParser(AnsiColorPalette.Vivid);

        var token = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[{sgrCode}mx")));

        Assert.Equal(Color.FromRgb(red, green, blue), token.Style.Foreground);
    }

    [Theory]
    [InlineData("1;31")]
    [InlineData("31;1")]
    public void BoldStandardColorUsesMudletBrightVariantRegardlessOfParameterOrder(string parameters)
    {
        var parser = new AnsiStreamParser(AnsiColorPalette.Vivid);

        var token = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[{parameters}mx")));

        Assert.True(token.Style.Bold);
        Assert.Equal(Color.FromRgb(255, 0, 0), token.Style.Foreground);
    }

    [Fact]
    public void BoldStateChangesPreviouslySelectedStandardColorAndIntensityResetRestoresIt()
    {
        var parser = new AnsiStreamParser(AnsiColorPalette.Vivid);

        var normal = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[31mA")));
        var bright = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[1mB")));
        var normalAgain = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[22mC")));

        Assert.Equal(Color.FromRgb(128, 0, 0), normal.Style.Foreground);
        Assert.Equal(Color.FromRgb(255, 0, 0), bright.Style.Foreground);
        Assert.Equal(Color.FromRgb(128, 0, 0), normalAgain.Style.Foreground);
    }

    [Fact]
    public void IntensityResetDoesNotDarkenExplicitBrightOrTrueColor()
    {
        var parser = new AnsiStreamParser(AnsiColorPalette.Vivid);

        var explicitBright = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[1;91;22mA")));
        var trueColor = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[38;2;12;34;56;1mB")));

        Assert.False(explicitBright.Style.Bold);
        Assert.Equal(Color.FromRgb(255, 0, 0), explicitBright.Style.Foreground);
        Assert.True(trueColor.Style.Bold);
        Assert.Equal(Color.FromRgb(12, 34, 56), trueColor.Style.Foreground);
    }

    [Fact]
    public void NamedSchemeAlsoChangesFirstSixteenColorsIn256Mode()
    {
        var parser = new AnsiStreamParser(AnsiColorPalette.Colorblind);

        var token = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[38;5;1mx")));

        Assert.Equal(Color.FromRgb(128, 128, 128), token.Style.Foreground);
    }

    [Theory]
    [InlineData(AnsiColorPalette.Warm)]
    [InlineData(AnsiColorPalette.Colorblind)]
    [InlineData(AnsiColorPalette.Vivid)]
    public void EverySchemeKeepsAnsiBlackReadableOnBlackBackground(string scheme)
    {
        var parser = new AnsiStreamParser(scheme);

        var normalBlack = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[30mx"))).Style.Foreground;
        var brightBlack = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[90mx"))).Style.Foreground;

        Assert.NotEqual(Colors.Black, normalBlack);
        Assert.NotEqual(Colors.Black, brightBlack);
    }

    [Fact]
    public void ColorblindSchemeContainsOnlyGrayscaleColors()
    {
        var parser = new AnsiStreamParser(AnsiColorPalette.Colorblind);

        for (var code = 30; code <= 37; code++)
        {
            var color = Assert.IsType<AnsiTextToken>(
                Assert.Single(parser.Feed($"{Esc}[{code}mx"))).Style.Foreground!.Value;
            Assert.Equal(color.R, color.G);
            Assert.Equal(color.G, color.B);
        }

        for (var code = 90; code <= 97; code++)
        {
            var color = Assert.IsType<AnsiTextToken>(
                Assert.Single(parser.Feed($"{Esc}[{code}mx"))).Style.Foreground!.Value;
            Assert.Equal(color.R, color.G);
            Assert.Equal(color.G, color.B);
        }
    }

    [Fact]
    public void NamedSchemeDoesNotChangeExplicitTrueColor()
    {
        var parser = new AnsiStreamParser(AnsiColorPalette.Vivid);

        var token = Assert.IsType<AnsiTextToken>(
            Assert.Single(parser.Feed($"{Esc}[38;2;10;20;30mx")));

        Assert.Equal(Color.FromRgb(10, 20, 30), token.Style.Foreground);
    }

    [Fact]
    public void ResetSequenceClearsStyle()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"{Esc}[1;32mA{Esc}[0mB");

        Assert.Equal(2, tokens.Count);
        var first = Assert.IsType<AnsiTextToken>(tokens[0]);
        var second = Assert.IsType<AnsiTextToken>(tokens[1]);
        Assert.True(first.Style.Bold);
        Assert.NotNull(first.Style.Foreground);
        Assert.False(second.Style.Bold);
        Assert.Null(second.Style.Foreground);
    }

    [Fact]
    public void TrueColorForegroundIsParsed()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"{Esc}[38;2;10;20;30mx");

        var text = Assert.IsType<AnsiTextToken>(Assert.Single(tokens));
        Assert.Equal(Color.FromRgb(10, 20, 30), text.Style.Foreground);
    }

    [Fact]
    public void Palette256ForegroundIsParsed()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"{Esc}[38;5;196mx");

        var text = Assert.IsType<AnsiTextToken>(Assert.Single(tokens));
        Assert.NotNull(text.Style.Foreground);
    }

    [Fact]
    public void SequenceSplitAcrossFeedCallsStillApplies()
    {
        var parser = new AnsiStreamParser();

        var firstBatch = parser.Feed($"{Esc}[3");
        var secondBatch = parser.Feed("2mhi");

        Assert.Empty(firstBatch);
        var text = Assert.IsType<AnsiTextToken>(Assert.Single(secondBatch));
        Assert.Equal("hi", text.Text);
        Assert.Equal(Color.FromRgb(13, 188, 121), text.Style.Foreground);
    }

    [Fact]
    public void TwoByteEscapeDoesNotSwallowFollowingText()
    {
        // ESC 'c' (RIS / full reset) is a complete two-byte escape sequence with no CSI
        // introducer. The parser must not keep scanning forward for an SGR terminator —
        // doing so used to eat real output (including parentheses) until a stray letter
        // happened to show up.
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"before{Esc}c(after 75%)");

        Assert.Equal("before(after 75%)", PlainText(tokens));
    }

    [Fact]
    public void SaveAndRestoreCursorEscapesDoNotSwallowText()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"a{Esc}7b{Esc}8c");

        Assert.Equal("abc", PlainText(tokens));
    }

    [Fact]
    public void CharsetDesignationConsumesExactlyOneExtraByte()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"a{Esc}(Bb");

        Assert.Equal("ab", PlainText(tokens));
    }

    [Fact]
    public void OscSequenceTerminatedByBelIsFullyConsumed()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"a{Esc}]0;window title\ab");

        Assert.Equal("ab", PlainText(tokens));
    }

    [Fact]
    public void OscSequenceTerminatedByStringTerminatorIsFullyConsumed()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"a{Esc}]0;window title{Esc}\\b");

        Assert.Equal("ab", PlainText(tokens));
    }

    [Fact]
    public void UnterminatedCsiSequenceIsEventuallyAbandonedRatherThanHangingForever()
    {
        // A CSI sequence with no legal final byte anywhere in this (unrealistically long)
        // run must not hold escape-state open forever — it should give up after a bound and
        // resume normal parsing, so trailing real text is never lost.
        var parser = new AnsiStreamParser();
        var garbageParams = string.Concat(Enumerable.Repeat("9", 300));

        var tokens = parser.Feed($"a{Esc}[{garbageParams} after-b");

        Assert.EndsWith("after-b", PlainText(tokens));
        Assert.StartsWith("a", PlainText(tokens));
    }

    [Fact]
    public void BackspaceRemovesPreviousCharacterWithinSameRun()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed("ab\bc");

        var text = Assert.IsType<AnsiTextToken>(Assert.Single(tokens));
        Assert.Equal("ac", text.Text);
    }

    [Fact]
    public void BackspaceRemovesLastCharacterOfPreviousStyledRun()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"{Esc}[32mab{Esc}[0m\b");

        var text = Assert.IsType<AnsiTextToken>(Assert.Single(tokens));
        Assert.Equal("a", text.Text);
    }

    [Fact]
    public void NewLineAndCarriageReturnProduceControlTokens()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed("a\r\nb");

        Assert.Equal(4, tokens.Count);
        Assert.IsType<AnsiTextToken>(tokens[0]);
        Assert.IsType<AnsiCarriageReturnToken>(tokens[1]);
        Assert.IsType<AnsiNewLineToken>(tokens[2]);
        Assert.IsType<AnsiTextToken>(tokens[3]);
    }

    [Fact]
    public void NulAndDelBytesAreDroppedWithoutAffectingSurroundingText()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed("a\0bc");

        var text = Assert.IsType<AnsiTextToken>(Assert.Single(tokens));
        Assert.Equal("abc", text.Text);
    }

    [Fact]
    public void RainbowStyleColorPerLetterKeepsEveryLetter()
    {
        // Common MUD stat-line decoration: every single letter gets its own SGR color code
        // immediately before it, e.g. "( 2) zszywana torba (75%)". No letter may be lost.
        var parser = new AnsiStreamParser();
        const string word = "zszywana torba";
        var colors = new[] { 31, 32, 33, 34, 35, 36, 91, 92, 93, 94, 95, 96, 37, 97 };

        var builder = new System.Text.StringBuilder();
        builder.Append("( 2) ");
        for (var i = 0; i < word.Length; i++)
        {
            builder.Append(Esc).Append('[').Append(colors[i % colors.Length]).Append('m').Append(word[i]);
        }

        builder.Append(Esc).Append("[0m (75%)");

        var tokens = parser.Feed(builder.ToString());

        Assert.Equal("( 2) zszywana torba (75%)", PlainText(tokens));
    }

    [Fact]
    public void RainbowStyleWith256AndTrueColorCodesKeepsEveryLetter()
    {
        var parser = new AnsiStreamParser();
        const string word = "zszywana";

        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < word.Length; i++)
        {
            if (i % 2 == 0)
            {
                builder.Append(Esc).Append("[38;5;").Append(20 + i).Append('m').Append(word[i]);
            }
            else
            {
                builder.Append(Esc).Append("[38;2;").Append(10 * i).Append(';').Append(20 * i).Append(';').Append(30 * i).Append('m').Append(word[i]);
            }
        }

        var tokens = parser.Feed(builder.ToString());

        Assert.Equal(word, PlainText(tokens));
    }

    [Fact]
    public void BoldPlusColorCombinedCodeDoesNotDropTheFollowingLetter()
    {
        var parser = new AnsiStreamParser();

        var tokens = parser.Feed($"{Esc}[1;32mz{Esc}[0;33mw{Esc}[22;34mn{Esc}[0mr");

        Assert.Equal("zwnr", PlainText(tokens));
    }
}
