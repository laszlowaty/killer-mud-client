using Android.Content;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.Android.Services;

public sealed class MobileSessionHost
{
    private readonly Context _context;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private MainWindowViewModel? _viewModel;

    public MobileSessionHost(Context context)
    {
        _context = context.ApplicationContext
            ?? throw new ArgumentException("Brak kontekstu aplikacji Android.", nameof(context));
    }

    public async Task<MainWindowViewModel> GetViewModelAsync(CancellationToken cancellationToken)
    {
        if (_viewModel is not null)
        {
            return _viewModel;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_viewModel is not null)
            {
                return _viewModel;
            }

            var appBaseDirectory = await MobileAssetBootstrap
                .EnsureMapAssetsAsync(_context, cancellationToken);
            var dataDirectory = Path.Combine(appBaseDirectory, "Data");
            Directory.CreateDirectory(dataDirectory);

            var viewModel = new MainWindowViewModel(
                profileService: new ProfileService(Path.Combine(dataDirectory, "Profiles")),
                settingsService: new AppSettingsService(dataDirectory),
                dockLayoutService: new DockLayoutService(dataDirectory),
                layoutPresetService: new LayoutPresetService(dataDirectory),
                appBaseDirectory: appBaseDirectory,
                passwordProtector: new AndroidKeystorePasswordProtector());

            viewModel.ShowTerminalVitalsBars = false;
            await viewModel.InitializeAsync(cancellationToken);
            _viewModel = viewModel;
            return viewModel;
        }
        finally
        {
            _initializationGate.Release();
        }
    }
}
