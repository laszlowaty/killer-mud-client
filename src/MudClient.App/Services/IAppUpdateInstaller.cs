using MudClient.App.Models;

namespace MudClient.App.Services;

public interface IAppUpdateInstaller
{
    bool CanInstallUpdates { get; }
    
    Task DownloadAndInstallUpdateAsync(AvailableUpdate update, CancellationToken cancellationToken);
}
