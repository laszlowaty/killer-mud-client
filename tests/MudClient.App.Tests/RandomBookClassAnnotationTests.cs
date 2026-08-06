using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

/// <summary>Covers the "Pokaż klasę losowych ksiąg magicznych" setting — splices " (Klasa)" right
/// after a recognized random-book name, in place, as it arrives. OnTextReceived posts the
/// (possibly rewritten) text via Dispatcher.UIThread, so these need a real headless dispatcher
/// pump.</summary>
[Collection(AvaloniaUiCollection.Name)]
public sealed class RandomBookClassAnnotationTests
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
            Path.GetTempPath(), "KillerMudClient_RandomBookClassTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(settingsService: new AppSettingsService(directory));
        var output = new List<string>();
        viewModel.OutputReceived += text => output.Add(text);
        return (viewModel, output, directory);
    }

    [AvaloniaFact]
    public async Task RandomBookName_WithSettingEnabled_AppendsClassRightAfterTheName()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            Assert.True(viewModel.AnnotateRandomBookClassEnabled);
            InvokeOnTextReceived(viewModel, "Widzisz tutaj: duża księga triumfu.\n");

            Assert.Contains(output, text => text.Contains("duża księga triumfu (Paladyn)."));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task RandomBookName_WithSettingDisabled_IsNotAnnotated()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            viewModel.AnnotateRandomBookClassEnabled = false;
            InvokeOnTextReceived(viewModel, "duża księga triumfu leży na ziemi.\n");

            Assert.Contains(output, text => text.Contains("duża księga triumfu leży na ziemi.\n"));
            Assert.DoesNotContain(output, text => text.Contains("(Paladyn)"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task UnrelatedLine_IsNeverAnnotated()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            InvokeOnTextReceived(viewModel, "Brakuje ci sily do dalszej walki.\n");

            Assert.DoesNotContain(output, text => text.Contains('('));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task IncompleteLine_WithoutNewline_IsForwardedUnmodifiedAndNeverAnnotated()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            InvokeOnTextReceived(viewModel, "duża księga triumfu");

            Assert.Contains(output, text => text == "duża księga triumfu");
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task MultipleBookNamesOnDifferentLinesInOneChunk_AreEachAnnotated()
    {
        var (viewModel, output, directory) = CreateViewModel();

        try
        {
            InvokeOnTextReceived(viewModel, "duża księga triumfu\nkolosalny tom magii\n");

            Assert.Contains(output, text => text.Contains("duża księga triumfu (Paladyn)"));
            Assert.Contains(output, text => text.Contains("kolosalny tom magii (Mag)"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
