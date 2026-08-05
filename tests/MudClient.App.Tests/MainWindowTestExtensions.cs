using MudClient.App.Views;

namespace MudClient.App.Tests;

internal static class MainWindowTestExtensions
{
    /// <summary>
    /// Closes a full application window and waits until its window-owned view model has released
    /// all asynchronous resources. Merely calling Close() is insufficient: MainWindow starts
    /// disposal from OnClosed, while Avalonia.Headless may immediately tear down the dispatcher.
    /// </summary>
    public static async Task CloseAndDisposeAsync(this MainWindow window)
    {
        window.Close();
        await window.ViewModelDisposalTask;
    }
}
