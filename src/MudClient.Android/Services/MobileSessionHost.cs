using Android.Content;
using Android.Util;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.Android.Services;

public sealed class MobileSessionHost
{
    private const string SessionPreferencesName = "mobile-session";
    private const string ActiveProfilePreferenceKey = "active-profile";

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

            viewModel.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(MainWindowViewModel.ActiveProfileName))
                {
                    PersistActiveProfile(viewModel.ActiveProfileName);
                }
            };
            RestoreActiveProfile(viewModel, profileService);

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

    private void RestoreActiveProfile(
        MainWindowViewModel viewModel,
        ProfileService profileService)
    {
        var profileName = GetSessionPreferences().GetString(
            ActiveProfilePreferenceKey,
            null);
        if (string.IsNullOrWhiteSpace(profileName) || !profileService.Exists(profileName))
        {
            return;
        }

        viewModel.SelectedProfileName = profileName;
        if (viewModel.SelectProfileCommand.CanExecute(null))
        {
            viewModel.SelectProfileCommand.Execute(null);
        }
    }

    private void PersistActiveProfile(string? profileName)
    {
        try
        {
            var editor = GetSessionPreferences().Edit();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                editor?.Remove(ActiveProfilePreferenceKey);
            }
            else
            {
                editor?.PutString(ActiveProfilePreferenceKey, profileName);
            }

            editor?.Apply();
        }
        catch (Exception exception)
        {
            // Losing this convenience state must not interrupt an active MUD session.
            Log.Warn("KillerMudClient", $"Nie udało się zapisać aktywnego profilu: {exception}");
        }
    }

    private ISharedPreferences GetSessionPreferences() =>
        _context.GetSharedPreferences(SessionPreferencesName, FileCreationMode.Private)
        ?? throw new InvalidOperationException(
            "Android nie udostępnił magazynu stanu sesji.");
}
