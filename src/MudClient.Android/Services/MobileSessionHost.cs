using Android.Content;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.Android.Services;

public sealed class MobileSessionHost
{
    private readonly Context _context;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private MainWindowViewModel? _viewModel;
    private Task? _viewModelInitialization;

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

            var appBaseDirectory = _context.FilesDir?.AbsolutePath
                ?? throw new InvalidOperationException(
                    "Android nie udostępnił katalogu danych aplikacji.");
            var dataDirectory = Path.Combine(appBaseDirectory, "Data");
            Directory.CreateDirectory(dataDirectory);

            Exception? importException = null;
            try
            {
                new SettingsBackupService(dataDirectory).ApplyPendingImport();
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException)
            {
                importException = exception;
            }

            var profileService = new ProfileService(Path.Combine(dataDirectory, "Profiles"));
            var viewModel = new MainWindowViewModel(
                profileService: profileService,
                settingsService: new AppSettingsService(dataDirectory),
                dockLayoutService: new DockLayoutService(dataDirectory),
                layoutPresetService: new LayoutPresetService(dataDirectory),
                appBaseDirectory: appBaseDirectory,
                passwordProtector: new AndroidKeystorePasswordProtector());

            viewModel.ShowTerminalVitalsBars = false;
            if (importException is not null)
            {
                viewModel.ReportSettingsImportError(importException);
            }

            _viewModel = viewModel;
            return viewModel;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async Task EnsureViewModelInitializedAsync(CancellationToken cancellationToken)
    {
        Task initialization;

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            var viewModel = _viewModel
                ?? throw new InvalidOperationException(
                    "Model sesji mobilnej nie został jeszcze utworzony.");
            initialization = _viewModelInitialization
                ??= InitializeViewModelAsync(viewModel);
        }
        finally
        {
            _initializationGate.Release();
        }

        await initialization.WaitAsync(cancellationToken);
    }

    private async Task InitializeViewModelAsync(MainWindowViewModel viewModel)
    {
        await MobileAssetBootstrap
            .EnsureMapAssetsAsync(_context, CancellationToken.None);
        await viewModel.InitializeAsync(CancellationToken.None);
    }

}
