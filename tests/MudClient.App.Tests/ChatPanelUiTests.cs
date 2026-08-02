using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Controls;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class ChatPanelUiTests
{
    [AvaloniaFact]
    public async Task ChatPanel_ShowsConversationReceivedBeforeWidgetWasAttached()
    {
        var directory = Directory.CreateTempSubdirectory("chat-panel-tests-").FullName;
        var viewModel = new MainWindowViewModel(
            profileService: new ProfileService(directory),
            settingsService: new AppSettingsService(directory),
            dockLayoutService: new DockLayoutService(directory));

        try
        {
            ReceiveLine(viewModel, "\u001b[33mAldar mówi 'Witaj.'\u001b[0m");
            ReceiveLine(viewModel, "Aldar przybywa z północy.");
            Dispatcher.UIThread.RunJobs();

            Assert.Single(viewModel.ChatHistory);

            var panel = new ChatPanelView { DataContext = viewModel };
            var window = new Window { Width = 500, Height = 300, Content = panel };
            window.Show();
            try
            {
                window.UpdateLayout();
                var output = panel.FindControl<MudOutputView>("ChatOutput")!;

                Assert.True(output.UpdateSearch("Witaj."));
                Assert.False(output.UpdateSearch("przybywa"));
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void ReceiveLine(MainWindowViewModel viewModel, string line)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "OnLineReceived",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [line]);
    }
}
