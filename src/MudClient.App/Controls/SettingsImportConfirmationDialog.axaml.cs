using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MudClient.App.Controls;

internal sealed partial class SettingsImportConfirmationDialog : Window
{
    public SettingsImportConfirmationDialog()
    {
        InitializeComponent();
        Opened += (_, _) => CancelButton.Focus();
    }

    internal static Task<bool> ShowAsync(Window owner) =>
        new SettingsImportConfirmationDialog().ShowDialog<bool>(owner);

    private void Cancel_OnClick(object? sender, RoutedEventArgs eventArgs) => Close(false);

    private void Import_OnClick(object? sender, RoutedEventArgs eventArgs) => Close(true);
}
