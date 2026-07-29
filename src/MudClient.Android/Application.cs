using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace MudClient.Android;

[Application]
public sealed class KillerMudAndroidApplication : AvaloniaAndroidApplication<MobileApp>
{
    public KillerMudAndroidApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .WithInterFont();
}
