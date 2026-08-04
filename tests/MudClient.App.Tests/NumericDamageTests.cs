using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

/// <summary>Covers the "Pokaż obrażenia liczbowo" setting — annotates recognized "you dealt
/// damage" combat lines with their numeric tier. EmitSystem (used by the annotation) posts via
/// Dispatcher.UIThread, so these need a real headless dispatcher pump.</summary>
[Collection(AvaloniaUiCollection.Name)]
public sealed class NumericDamageTests
{
    private static void InvokeOnLineReceived(MainWindowViewModel viewModel, string line)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "OnLineReceived", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(viewModel, [line]);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task DamageLine_WithSettingEnabled_AnnotatesWithNumericTier()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_NumericDamageTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(settingsService: new AppSettingsService(directory));
        var output = new List<string>();
        viewModel.OutputReceived += text => output.Add(text);

        try
        {
            Assert.True(viewModel.ShowNumericDamageEnabled);
            InvokeOnLineReceived(viewModel, "Ranisz golema swoim mieczem.");

            Assert.Contains(output, line => line.Contains("18 obrażeń"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task DamageLine_WithSettingDisabled_DoesNotAnnotate()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_NumericDamageTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(settingsService: new AppSettingsService(directory));
        var output = new List<string>();
        viewModel.OutputReceived += text => output.Add(text);

        try
        {
            viewModel.ShowNumericDamageEnabled = false;
            InvokeOnLineReceived(viewModel, "Ranisz golema swoim mieczem.");

            Assert.DoesNotContain(output, line => line.Contains("obrażeń"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ThirdPersonDamageLine_IsNeverAnnotated()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_NumericDamageTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(settingsService: new AppSettingsService(directory));
        var output = new List<string>();
        viewModel.OutputReceived += text => output.Add(text);

        try
        {
            InvokeOnLineReceived(viewModel, "Golem cię rani swoją pięścią.");

            Assert.DoesNotContain(output, line => line.Contains("obrażeń"));
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
