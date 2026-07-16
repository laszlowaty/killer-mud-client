using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MudClient.App.Controls;

internal sealed partial class DeleteConfirmationDialog : Window
{
    public DeleteConfirmationDialog()
    {
        InitializeComponent();
        Opened += (_, _) => CancelButton.Focus();
    }

    private DeleteConfirmationDialog(string itemType, string itemName)
        : this()
    {
        PromptText.Text = $"Czy na pewno chcesz usunąć {itemType} „{itemName}”?";
    }

    internal static Task<bool> ShowAsync(Window owner, string itemType, string itemName) =>
        new DeleteConfirmationDialog(itemType, itemName).ShowDialog<bool>(owner);

    private void Cancel_OnClick(object? sender, RoutedEventArgs eventArgs) => Close(false);

    private void Delete_OnClick(object? sender, RoutedEventArgs eventArgs) => Close(true);
}
