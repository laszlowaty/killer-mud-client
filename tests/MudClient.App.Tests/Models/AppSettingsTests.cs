using MudClient.App.Models;

namespace MudClient.App.Tests.Models;

public sealed class AppSettingsTests
{
    // ====================================================================
    // Default values
    // ====================================================================

    [Fact]
    public void DefaultCommandStackingSeparator_IsSemicolon()
    {
        Assert.Equal(";", AppSettings.DefaultCommandStackingSeparator);
    }

    [Fact]
    public void Constructor_DefaultCommandStackingSeparator()
    {
        var settings = new AppSettings();

        Assert.Equal(";", settings.CommandStackingSeparator);
    }

    [Fact]
    public void Constructor_DefaultOutputFontFamily()
    {
        var settings = new AppSettings();

        Assert.Equal("Consolas", settings.OutputFontFamily);
    }

    [Fact]
    public void Constructor_DefaultOutputFontSize()
    {
        var settings = new AppSettings();

        Assert.Equal(14, settings.OutputFontSize);
    }

    [Fact]
    public void Constructor_OutputWordWrapIsEnabledByDefault()
    {
        Assert.True(new AppSettings().OutputWordWrap);
    }

    [Fact]
    public void Constructor_ClearCommandInputAfterSendIsDisabledByDefault()
    {
        Assert.False(new AppSettings().ClearCommandInputAfterSend);
    }

    [Fact]
    public void Constructor_AutoAssistIsDisabledByDefault()
    {
        Assert.False(new AppSettings().AutoAssistEnabled);
    }

    [Fact]
    public void Constructor_GroupOrdersAreDisabledByDefault()
    {
        Assert.False(new AppSettings().GroupOrdersEnabled);
    }

    [Fact]
    public void Constructor_NumberedGroupMapMarkersAreDisabledByDefault()
    {
        Assert.False(new AppSettings().ShowGroupMembersAsNumbers);
    }

    [Fact]
    public void Constructor_LordModeIsDisabledByDefault()
    {
        Assert.False(new AppSettings().LordModeEnabled);
    }

    // ====================================================================
    // Property round-trip
    // ====================================================================

    [Fact]
    public void CommandStackingSeparator_RoundTrips()
    {
        var settings = new AppSettings();

        settings.CommandStackingSeparator = "|";
        Assert.Equal("|", settings.CommandStackingSeparator);

        settings.CommandStackingSeparator = "";
        Assert.Equal("", settings.CommandStackingSeparator);
    }

    [Fact]
    public void CommandStackingSeparator_CanSetToNull()
    {
        var settings = new AppSettings();

        settings.CommandStackingSeparator = null!;

        Assert.Null(settings.CommandStackingSeparator);
    }
}
