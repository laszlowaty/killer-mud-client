using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class ApplicationRestartServiceTests
{
    [Fact]
    public void WaitForPreviousProcessIfRequested_RemovesInternalArguments()
    {
        var result = ApplicationRestartService.WaitForPreviousProcessIfRequested(
        [
            "--profile", "test",
            ApplicationRestartService.WaitForProcessArgument, int.MaxValue.ToString(),
        ]);

        Assert.Equal(["--profile", "test"], result);
    }

    [Fact]
    public void CreateStartInfo_PreservesApplicationArgumentsAndAddsSingleWaitPair()
    {
        var startInfo = ApplicationRestartService.CreateStartInfo(
            1234,
            [
                "--profile", "test",
                ApplicationRestartService.WaitForProcessArgument, "999",
            ]);

        var arguments = startInfo.ArgumentList.ToArray();
        Assert.Contains("--profile", arguments);
        Assert.Contains("test", arguments);
        Assert.Equal(ApplicationRestartService.WaitForProcessArgument, arguments[^2]);
        Assert.Equal("1234", arguments[^1]);
        Assert.Single(arguments, argument => argument == ApplicationRestartService.WaitForProcessArgument);
        Assert.False(startInfo.UseShellExecute);
    }
}
