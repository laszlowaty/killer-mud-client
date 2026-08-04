using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

/// <summary>Covers the "Pokaż obrażenia liczbowo" setting — splices " (N)" onto the end of
/// recognized "you dealt damage" combat lines, in place, as they arrive. OnTextReceived posts the
/// (possibly rewritten) text via Dispatcher.UIThread, so these need a real headless dispatcher
/// pump.</summary>
[Collection(AvaloniaUiCollection.Name)]
public sealed class NumericDamageTests
{
    private static void InvokeOnTextReceived(MainWindowViewModel viewModel, string text)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "OnTextReceived", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(viewModel, [text]);
        Dispatcher.UIThread.RunJobs();
    }

    private static (MainWindowViewModel ViewModel, List<string> Output, string Directory) CreateViewModel()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_NumericDamageTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(settingsService: new AppSettingsService(directory));
        var output = new List<string>();
        viewModel.OutputReceived += text => output.Add(text);
        return (viewModel, output, directory);
    }

    [AvaloniaFact]
    public async Task SelfDamageLine_WithSettingEnabled_AppendsNumberToTheSameLine()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            Assert.True(viewModel.ShowNumericDamageEnabled);
            InvokeOnTextReceived(viewModel, "Ranisz golema swoim mieczem.\n");

            Assert.Contains(output, text => text.Contains("Ranisz golema swoim mieczem. (18)"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task TechniqueDamageLine_NamingYourTechnique_AppendsNumberToTheSameLine()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            InvokeOnTextReceived(
                viewModel, "Twoje miazdzace walniecie dewastuje sedziwego krasnoluda.\n");

            Assert.Contains(
                output,
                text => text.Contains(
                    "Twoje miazdzace walniecie dewastuje sedziwego krasnoluda. (44)"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task DamageLine_SplitAcrossTwoChunks_IsStillAnnotated()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            InvokeOnTextReceived(viewModel, "Ranisz gol");
            InvokeOnTextReceived(viewModel, "ema swoim mieczem.\n");

            Assert.Contains(output, text => text.Contains("ema swoim mieczem. (18)"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task DamageLine_WithSettingDisabled_IsNotAnnotated()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            viewModel.ShowNumericDamageEnabled = false;
            InvokeOnTextReceived(viewModel, "Ranisz golema swoim mieczem.\n");

            Assert.Contains(output, text => text.Contains("Ranisz golema swoim mieczem.\n"));
            Assert.DoesNotContain(output, text => text.Contains("(18)"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ThirdPersonDamageLine_WithoutYourTechniqueNamed_IsNeverAnnotated()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            InvokeOnTextReceived(viewModel, "Golem cię rani swoją pięścią.\n");

            Assert.DoesNotContain(output, text => text.Contains('('));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task IncompleteLine_WithoutNewline_IsForwardedUnmodified()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            InvokeOnTextReceived(viewModel, "Ranisz golema swoim mieczem.");

            Assert.Contains(output, text => text == "Ranisz golema swoim mieczem.");
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
