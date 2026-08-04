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

        // The TCP session lives in MobileSessionHost. Restarting only this service
        // after the process was killed could not recreate that connection safely.
        return StartCommandResult.NotSticky;
    }

    public override global::Android.OS.IBinder? OnBind(Intent? intent) => null;

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
