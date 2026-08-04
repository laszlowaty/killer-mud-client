using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class ToastExpirationUiTests
{
    [AvaloniaFact]
    public async Task Toast_DisappearsAfterConfiguredLifetime()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "KillerMudClient_ToastExpirationUiTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var viewModel = new MainWindowViewModel(
            profileService: new ProfileService(tempDirectory),
            settingsService: new AppSettingsService(tempDirectory),
            toastLifetime: TimeSpan.FromMilliseconds(25));

        try
        {
            Assert.Equal(TimeSpan.FromSeconds(3), MainWindowViewModel.DefaultToastLifetime);
            Assert.Single(viewModel.Toasts);

            await Task.Delay(100, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(viewModel.Toasts);
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
