using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace MudClient.Android.Services;

[Service(
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
[MetaData(
    "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE",
    Value = "maintain_user_started_interactive_mud_tcp_session_while_backgrounded")]
public sealed class ConnectionForegroundService : Service
{
    private const string ChannelId = "mud_connection";
    private const int NotificationId = 1001;
    private const string WakeLockTag = "KillerMudClient:MudConnection";
    private PowerManager.WakeLock? _wakeLock;

    public static void Start(Context context)
    {
        using var intent = new Intent(context, typeof(ConnectionForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }

    public static void Stop(Context context)
    {
        using var intent = new Intent(context, typeof(ConnectionForegroundService));
        context.StopService(intent);
    }

    public override StartCommandResult OnStartCommand(
        Intent? intent,
        StartCommandFlags flags,
        int startId)
    {
        EnsureNotificationChannel();
        var notification = BuildNotification();

        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            StartForeground(
                NotificationId,
                notification,
                ForegroundService.TypeSpecialUse);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        // A foreground service keeps the session process alive, but Android may
        // still suspend the CPU. Keep timer delays and TCP processing running
        // until disconnect stops this service.
        AcquireWakeLock();

        // The TCP session lives in MobileSessionHost. Restarting only this service
        // after the process was killed could not recreate that connection safely.
        return StartCommandResult.NotSticky;
    }

    public override global::Android.OS.IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        try
        {
            ReleaseWakeLock();
        }
        finally
        {
            base.OnDestroy();
        }
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        ReleaseWakeLock();
        base.OnTaskRemoved(rootIntent);

        // If the user swipes the app away from recent tasks, terminate the session
        // so the background connection doesn't keep running indefinitely.
        StopSelf();
        Java.Lang.JavaSystem.Exit(0);
    }

    private void AcquireWakeLock()
    {
        if (_wakeLock?.IsHeld == true)
        {
            return;
        }

        var powerManager = GetSystemService(PowerService) as PowerManager
            ?? throw new InvalidOperationException(
                "Android nie udostępnił menedżera zasilania.");
        _wakeLock ??= powerManager.NewWakeLock(
            WakeLockFlags.Partial,
            WakeLockTag);
        _wakeLock.SetReferenceCounted(false);
        _wakeLock.Acquire();
    }

    private void ReleaseWakeLock()
    {
        if (_wakeLock is null)
        {
            return;
        }

        if (_wakeLock.IsHeld)
        {
            _wakeLock.Release();
        }

        _wakeLock.Dispose();
        _wakeLock = null;
    }

    private void EnsureNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var notificationManager =
            GetSystemService(NotificationService) as NotificationManager;
        if (notificationManager is null)
        {
            throw new InvalidOperationException(
                "Android nie udostępnił menedżera powiadomień.");
        }

        using var channel = new NotificationChannel(
            ChannelId,
            "Aktywne połączenie z MUD-em",
            NotificationImportance.Low)
        {
            Description = "Utrzymuje połączenie z MUD-em, gdy aplikacja jest w tle.",
        };
        channel.SetShowBadge(false);
        notificationManager.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        using var openAppIntent = new Intent(this, typeof(MainActivity));
        openAppIntent.SetFlags(
            ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var pendingIntentFlags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            pendingIntentFlags |= PendingIntentFlags.Immutable;
        }

        var contentIntent = PendingIntent.GetActivity(
            this,
            0,
            openAppIntent,
            pendingIntentFlags);

        var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);

        return builder
            .SetContentTitle("KillerMudClient — połączono")
            .SetContentText("Połączenie z MUD-em pozostaje aktywne w tle.")
            .SetSmallIcon(Resource.Drawable.icon)
            .SetContentIntent(contentIntent)
            .SetCategory(Notification.CategoryService)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .Build();
    }
}
