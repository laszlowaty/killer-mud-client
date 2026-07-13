using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MudClient.App.ViewModels;

namespace MudClient.App.Controls;

/// <summary>
/// Reusable tree that groups items into nestable folders. It renders folder
/// chrome (name, badges, bulk actions) itself and delegates each leaf item to a
/// panel-supplied <see cref="ItemTemplate"/>. Folder actions are surfaced as
/// commands the host binds to its view model; the command parameter is always
/// the folder's <c>FolderNode</c>. Drag &amp; drop is layered on in a later stage.
/// </summary>
public partial class FolderTreeView : UserControl
{
    public static readonly StyledProperty<IEnumerable?> NodesProperty =
        AvaloniaProperty.Register<FolderTreeView, IEnumerable?>(nameof(Nodes));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<FolderTreeView, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<bool> SupportsEnableToggleProperty =
        AvaloniaProperty.Register<FolderTreeView, bool>(nameof(SupportsEnableToggle), defaultValue: true);

    public static readonly StyledProperty<ICommand?> CreateSubfolderCommandProperty =
        AvaloniaProperty.Register<FolderTreeView, ICommand?>(nameof(CreateSubfolderCommand));

    public static readonly StyledProperty<ICommand?> RenameFolderCommandProperty =
        AvaloniaProperty.Register<FolderTreeView, ICommand?>(nameof(RenameFolderCommand));

    public static readonly StyledProperty<ICommand?> DeleteFolderCommandProperty =
        AvaloniaProperty.Register<FolderTreeView, ICommand?>(nameof(DeleteFolderCommand));

    public static readonly StyledProperty<ICommand?> ToggleFolderGlobalCommandProperty =
        AvaloniaProperty.Register<FolderTreeView, ICommand?>(nameof(ToggleFolderGlobalCommand));

    public static readonly StyledProperty<ICommand?> ToggleFolderEnabledCommandProperty =
        AvaloniaProperty.Register<FolderTreeView, ICommand?>(nameof(ToggleFolderEnabledCommand));

    public FolderTreeView()
    {
        InitializeComponent();
    }

    /// <summary>Root nodes (folders and loose items) for one section/kind.</summary>
    public IEnumerable? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    /// <summary>Template used to render a leaf item's content.</summary>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Whether folders offer a bulk enable/disable toggle.</summary>
    public bool SupportsEnableToggle
    {
        get => GetValue(SupportsEnableToggleProperty);
        set => SetValue(SupportsEnableToggleProperty, value);
    }

    public ICommand? CreateSubfolderCommand
    {
        get => GetValue(CreateSubfolderCommandProperty);
        set => SetValue(CreateSubfolderCommandProperty, value);
    }

    public ICommand? RenameFolderCommand
    {
        get => GetValue(RenameFolderCommandProperty);
        set => SetValue(RenameFolderCommandProperty, value);
    }

    public ICommand? DeleteFolderCommand
    {
        get => GetValue(DeleteFolderCommandProperty);
        set => SetValue(DeleteFolderCommandProperty, value);
    }

    public ICommand? ToggleFolderGlobalCommand
    {
        get => GetValue(ToggleFolderGlobalCommandProperty);
        set => SetValue(ToggleFolderGlobalCommandProperty, value);
    }

    public ICommand? ToggleFolderEnabledCommand
    {
        get => GetValue(ToggleFolderEnabledCommandProperty);
        set => SetValue(ToggleFolderEnabledCommandProperty, value);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void FolderName_OnLostFocus(object? sender, RoutedEventArgs e) => CommitRename(sender);

    private void FolderName_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            CommitRename(sender);
        }
    }

    private void CommitRename(object? sender)
    {
        if (sender is Control { DataContext: FolderTreeNode { Folder: { } folder } } &&
            RenameFolderCommand is { } command &&
            command.CanExecute(folder))
        {
            command.Execute(folder);
        }
    }
}
