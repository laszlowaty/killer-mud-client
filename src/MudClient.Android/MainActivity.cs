using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using AndroidX.Activity;
using AndroidX.Core.View;
using SoftInput = Android.Views.SoftInput;

namespace MudClient.Android;

[Activity(
    Label = "KillerMudClient",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private ImeInsetsListener? _imeInsetsListener;
    private global::Android.Views.View? _decorView;
    private OnBackPressedCallback? _backPressedCallback;

    public MainActivity()
    {
        BackRequested += OnBackRequested;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _decorView = Window?.DecorView;
        if (_decorView is null)
        {
            return;
        }

        _imeInsetsListener = new ImeInsetsListener(OnImeInsetsChanged);
        ViewCompat.SetOnApplyWindowInsetsListener(
            _decorView,
            _imeInsetsListener);
        ViewCompat.RequestApplyInsets(_decorView);
    }

    protected override void OnDestroy()
    {
        if (_decorView is not null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(_decorView, null);
            _decorView = null;
        }

        _imeInsetsListener?.Dispose();
        _imeInsetsListener = null;
        base.OnDestroy();
    }

    protected override void OnStart()
    {
        base.OnStart();

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            _backPressedCallback ??= new KeepAppVisibleBackPressedCallback(
                NavigateBackToTerminal);
            OnBackPressedDispatcher.AddCallback(this, _backPressedCallback);
        }
    }

    protected override void OnStop()
    {
        _backPressedCallback?.Remove();
        base.OnStop();
    }

    private static void OnImeInsetsChanged(bool isVisible, int bottomInsetPixels)
    {
        if (Avalonia.Application.Current is MobileApp app)
        {
            app.SetImeState(isVisible, bottomInsetPixels);
        }
    }

    private static void OnBackRequested(
        object? sender,
        AndroidBackRequestedEventArgs eventArgs)
    {
        NavigateBackToTerminal();
        eventArgs.Handled = true;
    }

    private static void NavigateBackToTerminal()
    {
        if (Avalonia.Application.Current is MobileApp app)
        {
            app.TryNavigateBackToTerminal();
        }
    }

    private sealed class KeepAppVisibleBackPressedCallback(Action onBackPressed)
        : OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            onBackPressed();
        }
    }

    private sealed class ImeInsetsListener(
        Action<bool, int> onImeInsetsChanged)
        : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(
            global::Android.Views.View? view,
            WindowInsetsCompat? windowInsets)
        {
            if (windowInsets is null)
            {
                return null;
            }

            var imeType = WindowInsetsCompat.Type.Ime();
            var imeInsets = windowInsets.GetInsets(imeType);
            onImeInsetsChanged(
                windowInsets.IsVisible(imeType),
                imeInsets?.Bottom ?? 0);
            return windowInsets;
        }
    }
}
