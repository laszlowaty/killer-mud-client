using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Avalonia.Android;
using AndroidX.Activity;
using AndroidX.Core.View;
using MudClient.App.Models;
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
    internal const string PackageInstallStatusAction =
        "pl.killermud.client.action.PACKAGE_INSTALL_STATUS";

    private ImeInsetsObserver? _imeInsetsObserver;
    private global::Android.Views.View? _decorView;
    private OnBackPressedCallback? _backPressedCallback;

    public MainActivity()
    {
        BackRequested += OnBackRequested;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        HandlePackageInstallStatus(Intent);

        RequestNotificationPermission();

        _decorView = Window?.DecorView;
        if (_decorView is null)
        {
            return;
        }

        _imeInsetsObserver = new ImeInsetsObserver(
            _decorView,
            OnImeInsetsChanged);
        ViewCompat.SetOnApplyWindowInsetsListener(
            _decorView,
            _imeInsetsObserver);
        _decorView.ViewTreeObserver?.AddOnGlobalLayoutListener(
            _imeInsetsObserver);
        ViewCompat.RequestApplyInsets(_decorView);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandlePackageInstallStatus(intent);
    }

    protected override void OnDestroy()
    {
        if (_decorView is not null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(_decorView, null);
            if (_decorView.ViewTreeObserver?.IsAlive == true
                && _imeInsetsObserver is not null)
            {
                _decorView.ViewTreeObserver.RemoveOnGlobalLayoutListener(
                    _imeInsetsObserver);
            }

            _decorView = null;
        }

        _imeInsetsObserver?.Dispose();
        _imeInsetsObserver = null;
        base.OnDestroy();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);

        _imeInsetsObserver?.ResetLayoutBaseline();
        _decorView?.RequestLayout();
        if (_decorView is not null)
        {
            ViewCompat.RequestApplyInsets(_decorView);
        }

        if (Avalonia.Application.Current is MobileApp app)
        {
            app.RefreshLayoutAfterConfigurationChanged();
        }
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

    private void RequestNotificationPermission()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications)
                != Permission.Granted)
        {
            RequestPermissions(
                [global::Android.Manifest.Permission.PostNotifications],
                requestCode: 1001);
        }
    }

    private void HandlePackageInstallStatus(Intent? intent)
    {
        if (intent?.Action != PackageInstallStatusAction)
        {
            return;
        }

        var status = (PackageInstallStatus)intent.GetIntExtra(
            PackageInstaller.ExtraStatus,
            (int)PackageInstallStatus.Failure);
        if (status != PackageInstallStatus.PendingUserAction)
        {
            return;
        }

        var confirmationIntent = GetPackageInstallerConfirmationIntent(intent);
        if (confirmationIntent is not null)
        {
            StartActivity(confirmationIntent);
        }
    }

    private static Intent? GetPackageInstallerConfirmationIntent(Intent statusIntent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            using var intentClass = Java.Lang.Class.FromType(typeof(Intent));
            return statusIntent.GetParcelableExtra(Intent.ExtraIntent, intentClass) as Intent;
        }

#pragma warning disable CS0618 // Required on Android 12 and older.
        return statusIntent.GetParcelableExtra(Intent.ExtraIntent) as Intent;
#pragma warning restore CS0618
    }

    private sealed class KeepAppVisibleBackPressedCallback(Action onBackPressed)
        : OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            onBackPressed();
        }
    }

    private sealed class ImeInsetsObserver(
        global::Android.Views.View decorView,
        Action<bool, int> onImeInsetsChanged)
        : Java.Lang.Object,
          IOnApplyWindowInsetsListener,
          global::Android.Views.ViewTreeObserver.IOnGlobalLayoutListener
    {
        private readonly Rect _visibleWindowFrame = new();
        private bool? _lastVisibility;
        private int _lastBottomInset;
        private int _largestDecorHeight;

        public WindowInsetsCompat? OnApplyWindowInsets(
            global::Android.Views.View? view,
            WindowInsetsCompat? windowInsets)
        {
            if (windowInsets is null)
            {
                return null;
            }

            var imeType = WindowInsetsCompat.Type.Ime();
            if (windowInsets.IsVisible(imeType))
            {
                Publish(true, GetImeInsetAboveSystemBars(windowInsets));
            }
            else
            {
                // During the closing animation the insets flag can become false
                // before the window regains its full height. Measure the layout
                // before restoring the map so it cannot flash over the keyboard.
                OnGlobalLayout();
            }

            return windowInsets;
        }

        public void OnGlobalLayout()
        {
            var rootInsets = ViewCompat.GetRootWindowInsets(decorView);
            var imeType = WindowInsetsCompat.Type.Ime();
            if (rootInsets?.IsVisible(imeType) == true)
            {
                Publish(
                    true,
                    GetImeInsetAboveSystemBars(rootInsets));
                return;
            }

            decorView.GetWindowVisibleDisplayFrame(_visibleWindowFrame);
            var location = new int[2];
            decorView.GetLocationOnScreen(location);
            var decorBottom = location[1] + decorView.Height;
            var obscuredBottom = Math.Max(
                0,
                decorBottom - _visibleWindowFrame.Bottom);
            var systemBarsBottom = rootInsets?
                                       .GetInsets(WindowInsetsCompat.Type.SystemBars())
                                       ?.Bottom
                                   ?? 0;
            _largestDecorHeight = Math.Max(
                _largestDecorHeight,
                decorView.Height);
            var overlayInset = Math.Max(
                0,
                obscuredBottom - systemBarsBottom);
            var resizeInset = Math.Max(
                0,
                _largestDecorHeight - decorView.Height);
            var keyboardInset = Math.Max(overlayInset, resizeInset);
            var density = decorView.Resources?.DisplayMetrics?.Density ?? 1;
            var visibilityThreshold = 100 * Math.Max(1, density);

            Publish(
                keyboardInset >= visibilityThreshold,
                keyboardInset);
        }

        public void ResetLayoutBaseline()
        {
            _largestDecorHeight = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _visibleWindowFrame.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Publish(bool isVisible, int bottomInset)
        {
            bottomInset = Math.Max(0, bottomInset);
            if (_lastVisibility == isVisible
                && _lastBottomInset == bottomInset)
            {
                return;
            }

            _lastVisibility = isVisible;
            _lastBottomInset = bottomInset;
            onImeInsetsChanged(isVisible, bottomInset);
        }

        private static int GetImeInsetAboveSystemBars(
            WindowInsetsCompat windowInsets)
        {
            var imeBottom = windowInsets
                                .GetInsets(WindowInsetsCompat.Type.Ime())
                                ?.Bottom
                            ?? 0;
            var systemBarsBottom = windowInsets
                                       .GetInsets(WindowInsetsCompat.Type.SystemBars())
                                       ?.Bottom
                                   ?? 0;
            return (int)ViewportInsetCalculator.CalculateInsetAboveSystemBars(
                imeBottom,
                systemBarsBottom);
        }
    }
}
