using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MudClient.App.Controls;
using MudClient.App.Views;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class KilleropediaWorldMapPerformanceTests
{
    [AvaloniaFact]
    public void PanTransform_IsReusedAndCoalescedToOneUpdatePerFrame()
    {
        var view = new KilleropediaWorldMapView();
        var window = new Window { Width = 1000, Height = 700, Content = view };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var mapCanvas = view.FindControl<KilleropediaMapCanvas>("MapCanvas")!;
        var initialApplyCount = view.TransformApplyCount;

        for (var index = 0; index < 100; index++)
        {
            view.ScheduleTransform();
        }

        Assert.Equal(initialApplyCount, view.TransformApplyCount);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(initialApplyCount + 1, view.TransformApplyCount);
        Assert.Null(mapCanvas.RenderTransform);
        Assert.Equal(
            BitmapInterpolationMode.MediumQuality,
            RenderOptions.GetBitmapInterpolationMode(mapCanvas));
        window.Close();
    }

    [Fact]
    public void VisibleSourceRect_ContainsOnlyTheDisplayedFullResolutionFragment()
    {
        var source = KilleropediaMapCanvas.CalculateVisibleSourceRect(
            new Avalonia.Size(4202, 2498),
            new Avalonia.Size(1200, 700),
            scale: 2,
            offset: new Avalonia.Vector(-1800, -900));

        Assert.Equal(new Avalonia.Rect(900, 450, 600, 350), source);
        Assert.True(source.Width * source.Height < 4202 * 2498);
    }

}
