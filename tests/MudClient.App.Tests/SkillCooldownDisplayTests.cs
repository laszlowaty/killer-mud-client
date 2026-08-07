using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.Core.Gmcp;

namespace MudClient.App.Tests;

/// <summary>Covers OnSkillTimeoutsChanged's SkillsOnCooldown tracking (see Char.Skills.Timeout
/// GMCP handling in MainWindowViewModel) — only skills currently unusable are surfaced, as
/// "* skillname"; there is no separate "ready again" notice. The handler does its work inside a
/// Dispatcher.UIThread.Post, so these need a real headless dispatcher pump.</summary>
[Collection(AvaloniaUiCollection.Name)]
public sealed class SkillCooldownDisplayTests
{
    private static void InvokeSkillTimeoutsChanged(MainWindowViewModel viewModel, params SkillTimeoutEntry[] entries)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "OnSkillTimeoutsChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(viewModel, [(IReadOnlyList<SkillTimeoutEntry>)entries]);
        Dispatcher.UIThread.RunJobs();
    }

    private static MainWindowViewModel CreateViewModel()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_SkillCooldownDisplayTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new MainWindowViewModel(settingsService: new AppSettingsService(directory));
    }

    [AvaloniaFact]
    public async Task SkillOnCooldown_AppearsInSkillsOnCooldown()
    {
        var viewModel = CreateViewModel();
        try
        {
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("holy prayer", Timeout: true));

            Assert.Equal(["holy prayer"], viewModel.SkillsOnCooldown);
        }
        finally
        {
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SkillFlipsFromCooldownToFalse_RemovedFromSkillsOnCooldown()
    {
        var viewModel = CreateViewModel();
        try
        {
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("holy prayer", Timeout: true));
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("holy prayer", Timeout: false));

            Assert.Empty(viewModel.SkillsOnCooldown);
        }
        finally
        {
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SkillOnCooldownDropsOutOfSnapshot_RemovedFromSkillsOnCooldown()
    {
        var viewModel = CreateViewModel();
        try
        {
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("call avatar", Timeout: true));
            // Next snapshot no longer mentions "call avatar" at all — the server stopped
            // reporting it because its cooldown ended.
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("torment", Timeout: true));

            Assert.DoesNotContain("call avatar", viewModel.SkillsOnCooldown);
            Assert.Contains("torment", viewModel.SkillsOnCooldown);
        }
        finally
        {
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SkillNeverOnCooldown_ReportingFalseDoesNotAddIt()
    {
        var viewModel = CreateViewModel();
        try
        {
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("torment", Timeout: false));

            Assert.Empty(viewModel.SkillsOnCooldown);
        }
        finally
        {
            await viewModel.DisposeAsync();
        }
    }
}
