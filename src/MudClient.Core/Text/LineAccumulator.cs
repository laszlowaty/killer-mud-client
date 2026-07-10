using System.Text;

namespace MudClient.Core.Text;

public sealed class LineAccumulator
{
    private readonly StringBuilder _currentLine = new();

    public IEnumerable<string> Feed(string text)
    {
        foreach (var character in text)
        {
            if (character == '\r')
            {
                continue;
            }

            if (character == '\n')
            {
                yield return _currentLine.ToString();
                _currentLine.Clear();
                continue;
            }

            _currentLine.Append(character);
        }
    }

    public string CurrentPrompt => _currentLine.ToString();
}
