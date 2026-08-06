using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.Core.Gmcp;

namespace MudClient.App.Tests;

/// <summary>Covers OnSkillTimeoutsChanged's cooldown → ready-again transition detection (see
/// Skills.Timeout GMCP handling in MainWindowViewModel). The handler does its work inside a
/// Dispatcher.UIThread.Post, so these need a real headless dispatcher pump.</summary>
[Collection(AvaloniaUiCollection.Name)]
public sealed class SkillReadyNoticeTests
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
            Path.GetTempPath(), "KillerMudClient_SkillReadyNoticeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new MainWindowViewModel(settingsService: new AppSettingsService(directory));
    }

    [AvaloniaFact]
    public async Task SkillFirstReportedOnCooldown_DoesNotAnnounceReady()
    {
        var viewModel = CreateViewModel();
        try
        {
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("torment", Timeout: true));

            Assert.Empty(viewModel.SkillReadyNotices);
        }
        finally
        {
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SkillFlipsFromCooldownToFalse_AnnouncesReady()
    {
        var viewModel = CreateViewModel();
        try
        {
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("torment", Timeout: true));
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("torment", Timeout: false));

            var notice = Assert.Single(viewModel.SkillReadyNotices);
            Assert.Equal("torment", notice.Name);
        }
        finally
        {
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SkillOnCooldownDropsOutOfSnapshot_AnnouncesReady()
    {
        var viewModel = CreateViewModel();
        try
        {
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("call avatar", Timeout: true));
            // Next snapshot no longer mentions "call avatar" at all — the server stopped
            // reporting it because its cooldown ended.
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("torment", Timeout: true));

            var notice = Assert.Single(viewModel.SkillReadyNotices);
            Assert.Equal("call avatar", notice.Name);
        }
        finally
        {
            await viewModel.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SkillNeverOnCooldown_ReportingFalseDoesNotAnnounce()
    {
        var viewModel = CreateViewModel();
        try
        {
            InvokeSkillTimeoutsChanged(viewModel, new SkillTimeoutEntry("torment", Timeout: false));

            Assert.Empty(viewModel.SkillReadyNotices);
        }
        finally
        {
            await viewModel.DisposeAsync();
        }
    }
}
