namespace MudClient.Core.Map;

public sealed class MapLoadException : Exception
{
    public MapLoadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
