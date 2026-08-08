using Android.App;
using Android.Content;
using Android.Content.PM;
using MudClient.App.Models;
using MudClient.App.Services;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;

namespace MudClient.Android.Services;

public sealed class AndroidAppUpdateInstaller : IAppUpdateInstaller
{
    private const int InstallStatusRequestCode = 42018;
    private readonly Context _context;
    private readonly HttpClient _httpClient;

    public AndroidAppUpdateInstaller(Context context)
    {
        _context = context;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("KillerMudClient-UpdateCheck");
    }

    public bool CanInstallUpdates => true;

    public async Task DownloadAndInstallUpdateAsync(AvailableUpdate update, CancellationToken cancellationToken)
    {
        var apkName = $"KillerMudClient-{update.Version}-android.apk";
        var downloadUrl = $"https://github.com/laszlowaty/killer-mud-client/releases/download/v{update.Version}/{apkName}";

        var cacheDir = _context.CacheDir ?? throw new InvalidOperationException("No cache dir");
        var apkFile = new Java.IO.File(cacheDir, apkName);

        if (apkFile.Exists())
        {
            apkFile.Delete();
        }

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var fileStream = File.Create(apkFile.AbsolutePath))
        {
            await response.Content.CopyToAsync(fileStream, cancellationToken);
        }

        var packageInstaller = _context.PackageManager?.PackageInstaller
            ?? throw new InvalidOperationException("Android nie udostępnił instalatora pakietów.");
        using var parameters = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
        parameters.SetAppPackageName(_context.PackageName);

        var sessionId = packageInstaller.CreateSession(parameters);
        var committed = false;
        try
        {
            using var session = packageInstaller.OpenSession(sessionId);
            await using (var apkStream = File.OpenRead(apkFile.AbsolutePath))
            await using (var sessionStream = session.OpenWrite(apkName, 0, apkStream.Length))
            {
                await apkStream.CopyToAsync(sessionStream, cancellationToken);
                session.Fsync(sessionStream);
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var statusIntent = new Intent(_context, typeof(MainActivity));
            statusIntent.SetAction(MainActivity.PackageInstallStatusAction);
            statusIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);

            var pendingIntentFlags = PendingIntentFlags.UpdateCurrent;
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                pendingIntentFlags |= PendingIntentFlags.Mutable;
            }

            using var statusPendingIntent = PendingIntent.GetActivity(
                _context,
                InstallStatusRequestCode,
                statusIntent,
                pendingIntentFlags)
                ?? throw new InvalidOperationException("Android nie utworzył potwierdzenia instalacji.");

            session.Commit(statusPendingIntent.IntentSender);
            committed = true;
        }
        finally
        {
            if (!committed)
            {
                packageInstaller.AbandonSession(sessionId);
            }

            apkFile.Delete();
        }
    }
}
