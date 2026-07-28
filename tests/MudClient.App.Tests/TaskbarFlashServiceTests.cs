using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MudClient.App.Services;
using Xunit;

namespace MudClient.App.Tests;

/// <summary>
/// TaskbarFlashService is a no-op on every platform except Windows (see its own doc comment).
/// These just prove Start/Stop never throw — on non-Windows CI that's trivially true; on Windows
/// it exercises the real Win32 P/Invoke path against a headless window's platform handle (which
/// may itself be unavailable, in which case the service quietly no-ops).
/// </summary>
[Collection(AvaloniaUiCollection.Name)]
public sealed class TaskbarFlashServiceTests
{
    [AvaloniaFact]
    public void StartThenStop_DoesNotThrow()
    {
        var window = new Window();
        window.Show();

        try
        {
            var exception = Record.Exception(() =>
            {
                TaskbarFlashService.Start(window);
                TaskbarFlashService.Stop(window);
            });

            Assert.Null(exception);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_WithoutPriorStart_DoesNotThrow()
    {
        var window = new Window();
        window.Show();

        try
        {
            var exception = Record.Exception(() => TaskbarFlashService.Stop(window));

            Assert.Null(exception);
        }
        finally
        {
            window.Close();
        }
    }
}
