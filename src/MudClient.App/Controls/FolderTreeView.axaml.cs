using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Controls;

/// <summary>
/// Reusable tree that groups items into nestable folders. It renders folder
/// chrome (name, badges, bulk actions) itself and delegates each leaf item to a
/// panel-supplied <see cref="ItemTemplate"/>. Folder actions are surfaced as
/// commands the host binds to its view model; the command parameter is always
/// the folder's <c>FolderNode</c>. Items and folders can be dragged into folders;
/// the actual move is delegated to the host command so the control stays generic.
/// </summary>
public partial class FolderTreeView : UserControl
{
    public static readonly StyledProperty<IEnumerable?> NodesProperty =
        AvaloniaProperty.Register<FolderTreeView, IEnumerable?>(nameof(Nodes));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<FolderTreeView, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<bool> SupportsEnableToggleProperty =
        AvaloniaProperty.Register<FolderTreeView, bool>(nameof(SupportsEnableToggle), defaultValue: true);

    public static readonly StyledProperty<bool> SupportsExportProperty =
        AvaloniaProperty.Register<FolderTreeView, bool>(nameof(SupportsExport));

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

    public static readonly StyledProperty<ICommand?> MoveIntoFolderCommandProperty =
        AvaloniaProperty.Register<FolderTreeView, ICommand?>(nameof(MoveIntoFolderCommand));

    private FolderTreeNode? _dragSource;
    private PointerPressedEventArgs? _dragStartEvent;
    private Point _dragStart;
    private bool _dragInProgress;

    public FolderTreeView()
    {
        InitializeComponent();
    }

    public event EventHandler<FolderExportRequestedEventArgs>? FolderExportRequested;

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

    public bool SupportsExport
    {
        get => GetValue(SupportsExportProperty);
        set => SetValue(SupportsExportProperty, value);
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

    public ICommand? MoveIntoFolderCommand
    {
        get => GetValue(MoveIntoFolderCommandProperty);
        set => SetValue(MoveIntoFolderCommandProperty, value);
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

    private void ExportFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FolderTreeNode { Folder: { } folder } })
        {
            FolderExportRequested?.Invoke(this, new FolderExportRequestedEventArgs(folder));
        }
    }

    private void Node_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: FolderTreeNode node } &&
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragSource = node;
            _dragStartEvent = e;
            _dragStart = e.GetPosition(this);
        }
    }

    private async void Node_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragSource is null || _dragStartEvent is null || _dragInProgress || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStart.X) < 4 && Math.Abs(current.Y - _dragStart.Y) < 4)
        {
            return;
        }

        _dragInProgress = true;
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText("KillerMudClient/FolderTreeNode"));
            await DragDrop.DoDragDropAsync(_dragStartEvent, data, DragDropEffects.Move);
        }
        finally
        {
            _dragInProgress = false;
            _dragSource = null;
            _dragStartEvent = null;
        }
    }

    private void Node_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = CanDropOn(sender) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Node_OnDrop(object? sender, DragEventArgs e)
    {
        if (!CanDropOn(sender) ||
            sender is not Control { DataContext: FolderTreeNode { Folder: { } target } } ||
            _dragSource is null ||
            MoveIntoFolderCommand is not { } command)
        {
            return;
        }

        var source = _dragSource.IsFolder ? _dragSource.Folder : _dragSource.Content;
        if (source is null)
        {
            return;
        }

        var request = new FolderMoveRequest(source, target);
        if (command.CanExecute(request))
        {
            command.Execute(request);
            e.Handled = true;
        }
    }

    private bool CanDropOn(object? sender) =>
        _dragSource is not null &&
        sender is Control { DataContext: FolderTreeNode { IsFolder: true, Folder: not null } };
}

public sealed class FolderExportRequestedEventArgs(FolderNode folder) : EventArgs
{
    public FolderNode Folder { get; } = folder;
}
