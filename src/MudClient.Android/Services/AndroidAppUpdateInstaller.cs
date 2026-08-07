using Android.Content;
using AndroidX.Core.Content;
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

        var uri = FileProvider.GetUriForFile(
            _context,
            "pl.laszlowaty.killermudclient.fileprovider",
            apkFile);

        var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(uri, "application/vnd.android.package-archive");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        intent.AddFlags(ActivityFlags.NewTask);

        _context.StartActivity(intent);
    }
}
