using MudClient.Core.Automation;

namespace MudClient.Core.Tests;

public sealed class ChatLinePolicyTests
{
    [Theory]
    // say — third person and self. Nothing may follow the closing quote (the pattern is
    // anchored to end-of-line), so any trailing punctuation belongs inside the quoted message.
    [InlineData("Ala mówi 'witam wszystkich'")]
    [InlineData("Mówisz 'witam wszystkich'")]
    // say — race/class speech-verb variants (miauczy = "meows", szczeka = "barks", etc.).
    [InlineData("Kot miauczy 'witam wszystkich'")]
    [InlineData("Miauczysz 'witam wszystkich'")]
    // sayto / ask.
    [InlineData("Ala pyta Bob 'gdzie jestes?'")]
    [InlineData("Pytasz Bob 'gdzie jestes?'")]
    // yell.
    [InlineData("Wykrzykujesz 'na pomoc!'")]
    [InlineData("Ala wykrzykuje 'na pomoc!'")]
    // shout.
    [InlineData("Krzyczysz 'uwaga!'")]
    [InlineData("Ala krzyczy 'uwaga!'")]
    [InlineData("Wrzeszczysz 'uwaga!'")]
    [InlineData("Ala wrzeszczy 'uwaga!'")]
    // tell / clantell / grouptell — generic bracketed channel form; nothing follows "(.+)" here,
    // so trailing punctuation is fine.
    [InlineData("[Ala]: witam")]
    [InlineData("[Klan]: witam wszystkim!")]
    [InlineData("[Druzyna]: witam")]
    public void IsCommunicationLine_RecognizesEveryChannelType(string line)
    {
        Assert.True(ChatLinePolicy.IsCommunicationLine(line));
    }

    [Fact]
    public void IsCommunicationLine_StripsAnsiBeforeMatching()
    {
        var esc = Convert.ToChar(27);
        var colored = string.Concat(esc, "[32mAla mówi 'witam wszystkich'", esc, "[0m");

        Assert.True(ChatLinePolicy.IsCommunicationLine(colored));
    }

    [Theory]
    [InlineData("Pokój jest ciemny.")]
    [InlineData("Ala wychodzi na północ.")]
    [InlineData("")]
    public void IsCommunicationLine_OrdinaryOutput_ReturnsFalse(string line)
    {
        Assert.False(ChatLinePolicy.IsCommunicationLine(line));
    }
}
