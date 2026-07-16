using System.Diagnostics;

namespace MudClient.App.Services;

public interface IExternalLinkService
{
    void Open(Uri uri);
}

internal sealed class ExternalLinkService : IExternalLinkService
{
    public void Open(Uri uri)
    {
        if (Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }) is null)
        {
            throw new InvalidOperationException("Nie udało się otworzyć przeglądarki.");
        }
    }
}
