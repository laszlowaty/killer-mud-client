using Android.App;
using Android.Content;
using Android.OS;

namespace MudClient.Android.Services;

internal static class AndroidApplicationRestartService
{
    private const int RestartRequestCode = 42017;

    public static void ScheduleRestartAndExit(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var restartIntent = new Intent(context, typeof(MainActivity));
        restartIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

        using var restartPendingIntent = PendingIntent.GetActivity(
            context,
            RestartRequestCode,
            restartIntent,
            PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable)
            ?? throw new InvalidOperationException("Android nie utworzył żądania restartu aplikacji.");

        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager
            ?? throw new InvalidOperationException("Android nie udostępnił usługi restartu aplikacji.");
        alarmManager.Set(
            AlarmType.ElapsedRealtime,
            SystemClock.ElapsedRealtime() + 750,
            restartPendingIntent);

        Process.KillProcess(Process.MyPid());
    }
}
