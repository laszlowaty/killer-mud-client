using MudClient.Core.Text;

namespace MudClient.Core.Tests;

public sealed class ChatLineClassifierTests
{
    [Theory]
    [InlineData("Aldar mówi 'Witaj.'")]
    [InlineData("Aldar pyta cię cicho 'Dokąd idziesz?'")]
    [InlineData("Mówisz do Aldara 'Witaj.'")]
    [InlineData("Krzyczysz 'Pomocy!'")]
    [InlineData("Aldar wykrzykuje donośnie 'Naprzód!'")]
    [InlineData("[Aldar]: wiadomość dla grupy")]
    [InlineData("\u001b[33mAldar mówi 'Kolorowa wiadomość.'\u001b[0m")]
    public void IsChatLine_AcceptsMudletKchatConversationForms(string line)
    {
        Assert.True(ChatLineClassifier.IsChatLine(line));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Aldar przybywa z północy.")]
    [InlineData("Masz 100 punktów życia.")]
    [InlineData("[Aldar]:")]
    [InlineData("Aldar mówi bez cudzysłowu.")]
    public void IsChatLine_RejectsNonConversationOutput(string line)
    {
        Assert.False(ChatLineClassifier.IsChatLine(line));
    }
}
