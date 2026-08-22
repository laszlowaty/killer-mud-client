using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.Core.Scripting;

namespace MudClient.App.Controls;

public sealed class FullscreenTextEditor : UserControl
{
    private const double DefaultMaximumHeight = 420;
    private static readonly JavaScriptRunner JavaScriptValidator = new();
    private static readonly IBrush ValidSyntaxBrush = new SolidColorBrush(Color.Parse("#2E7D32"));
    private static readonly IBrush InvalidSyntaxBrush = new SolidColorBrush(Color.Parse("#C62828"));
    private static WeakReference<FullscreenTextEditor>? _openEditor;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FullscreenTextEditor, string?>(
            nameof(Text),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<FullscreenTextEditor, string?>(nameof(PlaceholderText));

    public static readonly StyledProperty<string> EditorTitleProperty =
        AvaloniaProperty.Register<FullscreenTextEditor, string>(nameof(EditorTitle), "Edycja");

    public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
        AvaloniaProperty.Register<FullscreenTextEditor, TextWrapping>(
            nameof(TextWrapping),
            TextWrapping.Wrap);

    public static readonly StyledProperty<bool> IsJavaScriptProperty =
        AvaloniaProperty.Register<FullscreenTextEditor, bool>(nameof(IsJavaScript));

    private readonly JavaScriptTextBox _inlineEditor;
    private readonly TextBlock _validationMessage;
    private Panel? _fullscreenHost;
    private Control? _fullscreenOverlay;
    private JavaScriptTextBox? _fullscreenEditor;
    private TextBlock? _fullscreenValidationMessage;
    private bool _synchronizingText;

    public FullscreenTextEditor()
    {
        MaxHeight = DefaultMaximumHeight;

        _inlineEditor = CreateEditor("InlineEditor");
        _validationMessage = CreateValidationMessage("JavaScriptValidationMessage");

        var expandButton = new Button
        {
            Name = "ExpandEditorButton",
            Content = "⛶",
            Padding = new Thickness(7, 3),
            Margin = new Thickness(0, 6, 6, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        ToolTip.SetTip(expandButton, "Edytuj na pełnym ekranie");
        expandButton.Classes.Add("mud-small");
        expandButton.Click += (_, _) => OpenFullscreenEditor();

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Background = Brushes.White,
        };
        layout.Children.Add(_inlineEditor);
        layout.Children.Add(expandButton);
        Grid.SetRow(_validationMessage, 1);
        layout.Children.Add(_validationMessage);
        Content = layout;

        UpdateValidation();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string EditorTitle
    {
        get => GetValue(EditorTitleProperty);
        set => SetValue(EditorTitleProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public bool IsJavaScript
    {
        get => GetValue(IsJavaScriptProperty);
        set => SetValue(IsJavaScriptProperty, value);
    }

    public string? JavaScriptValidationError { get; private set; }

    public bool IsFullscreen => _fullscreenOverlay is not null;

    public static bool TryCloseOpenEditor()
    {
        if (_openEditor is null || !_openEditor.TryGetTarget(out var editor) || !editor.IsFullscreen)
        {
            return false;
        }

        editor.CloseFullscreenEditor();
        return true;
    }

    public void OpenFullscreenEditor()
    {
        if (IsFullscreen)
        {
            return;
        }

        if (_openEditor is not null && _openEditor.TryGetTarget(out var previousEditor))
        {
            previousEditor.CloseFullscreenEditor();
        }

        var topLevel = TopLevel.GetTopLevel(this);
        var fullscreenHost = topLevel is null ? null : FindFullscreenHost(topLevel);
        if (fullscreenHost is null)
        {
            return;
        }

        _fullscreenEditor = CreateEditor("FullscreenEditor");
        _fullscreenEditor.Text = Text;
        _fullscreenEditor.HorizontalAlignment = HorizontalAlignment.Stretch;
        _fullscreenEditor.VerticalAlignment = VerticalAlignment.Stretch;
        _fullscreenValidationMessage = CreateValidationMessage("FullscreenJavaScriptValidationMessage");
        ApplyValidationMessage(_fullscreenValidationMessage);

        var closeButton = new Button
        {
            Name = "CloseFullscreenEditorButton",
            Content = "Gotowe",
            MinWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        closeButton.Click += (_, _) => CloseFullscreenEditor();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock
        {
            Text = EditorTitle,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(closeButton, 1);
        header.Children.Add(closeButton);

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 10,
            Margin = new Thickness(14),
            Background = Brushes.White,
        };
        content.Children.Add(header);
        Grid.SetRow(_fullscreenEditor, 1);
        content.Children.Add(_fullscreenEditor);
        Grid.SetRow(_fullscreenValidationMessage, 2);
        content.Children.Add(_fullscreenValidationMessage);

        var backdrop = new Border
        {
            Name = "FullscreenEditorOverlay",
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = content,
        };
        backdrop.SetValue(Panel.ZIndexProperty, int.MaxValue);
        backdrop.KeyDown += FullscreenOverlay_OnKeyDown;

        if (fullscreenHost is Grid hostGrid)
        {
            Grid.SetRow(backdrop, 0);
            Grid.SetRowSpan(backdrop, Math.Max(1, hostGrid.RowDefinitions.Count));
            Grid.SetColumn(backdrop, 0);
            Grid.SetColumnSpan(backdrop, Math.Max(1, hostGrid.ColumnDefinitions.Count));
        }

        _fullscreenHost = fullscreenHost;
        _fullscreenOverlay = backdrop;
        _openEditor = new WeakReference<FullscreenTextEditor>(this);
        fullscreenHost.Children.Add(backdrop);
        Dispatcher.UIThread.Post(() => _fullscreenEditor?.Focus());
    }

    public void CloseFullscreenEditor()
    {
        if (_fullscreenOverlay is null)
        {
            return;
        }

        if (_fullscreenEditor is not null)
        {
            SetCurrentValue(TextProperty, _fullscreenEditor.Text);
        }

        _fullscreenHost?.Children.Remove(_fullscreenOverlay);
        _fullscreenHost = null;
        _fullscreenOverlay = null;
        _fullscreenEditor = null;
        _fullscreenValidationMessage = null;
        _inlineEditor.Focus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty && !_synchronizingText)
        {
            SynchronizeEditors(change.NewValue as string);
            UpdateValidation();
        }
        else if (change.Property == PlaceholderTextProperty)
        {
            _inlineEditor.PlaceholderText = PlaceholderText;
            if (_fullscreenEditor is not null)
            {
                _fullscreenEditor.PlaceholderText = PlaceholderText;
            }
        }
        else if (change.Property == TextWrappingProperty)
        {
            ApplyWrapping(_inlineEditor);
            if (_fullscreenEditor is not null)
            {
                ApplyWrapping(_fullscreenEditor);
            }
        }
        else if (change.Property == IsJavaScriptProperty)
        {
            _inlineEditor.IsSyntaxHighlightingEnabled = IsJavaScript;
            if (_fullscreenEditor is not null)
            {
                _fullscreenEditor.IsSyntaxHighlightingEnabled = IsJavaScript;
            }

            UpdateValidation();
        }
        else if (change.Property == EditorTitleProperty)
        {
            UpdateValidation();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        CloseFullscreenEditor();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private JavaScriptTextBox CreateEditor(string name)
    {
        var editor = new JavaScriptTextBox
        {
            Name = name,
            AcceptsReturn = true,
            Padding = new Thickness(8, 8, 40, 8),
            PlaceholderText = PlaceholderText,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            CaretBrush = Brushes.Black,
            SelectionBrush = new SolidColorBrush(Color.Parse("#6EA8FE")),
            IsSyntaxHighlightingEnabled = IsJavaScript,
        };
        editor.Classes.Add("automation-editor");
        editor.TextChanged += Editor_OnTextChanged;
        ApplyWrapping(editor);
        return editor;
    }

    private void ApplyWrapping(TextBox editor)
    {
        var wraps = TextWrapping != TextWrapping.NoWrap;
        editor.TextWrapping = TextWrapping;
        editor.SetValue(
            ScrollViewer.HorizontalScrollBarVisibilityProperty,
            wraps ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
        editor.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
    }

    private void Editor_OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        if (_synchronizingText || sender is not TextBox editor)
        {
            return;
        }

        _synchronizingText = true;
        try
        {
            SetCurrentValue(TextProperty, editor.Text);
            SynchronizeEditors(editor.Text, editor);
        }
        finally
        {
            _synchronizingText = false;
        }

        UpdateValidation();
    }

    private void SynchronizeEditors(string? text, TextBox? source = null)
    {
        var wasSynchronizing = _synchronizingText;
        _synchronizingText = true;
        try
        {
            SetEditorTextUnlessSource(_inlineEditor, text, source);
            if (_fullscreenEditor is not null)
            {
                SetEditorTextUnlessSource(_fullscreenEditor, text, source);
            }
        }
        finally
        {
            _synchronizingText = wasSynchronizing;
        }
    }

    private static void SetEditorTextUnlessSource(TextBox editor, string? text, TextBox? source)
    {
        if (!ReferenceEquals(editor, source) && editor.Text != text)
        {
            editor.Text = text;
        }
    }

    private void UpdateValidation()
    {
        JavaScriptValidationError = IsJavaScript
            ? JavaScriptValidator.Validate(EditorTitle, Text ?? string.Empty)
            : null;
        ApplyValidationMessage(_validationMessage);
        if (_fullscreenValidationMessage is not null)
        {
            ApplyValidationMessage(_fullscreenValidationMessage);
        }
    }

    private void ApplyValidationMessage(TextBlock message)
    {
        message.IsVisible = IsJavaScript;
        message.Text = JavaScriptValidationError ?? "Składnia JavaScript poprawna";
        message.Foreground = JavaScriptValidationError is null ? ValidSyntaxBrush : InvalidSyntaxBrush;
    }

    private static TextBlock CreateValidationMessage(string name) => new()
    {
        Name = name,
        Margin = new Thickness(2, 4, 2, 0),
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
    };

    private static Panel? FindFullscreenHost(TopLevel topLevel)
    {
        Control current = topLevel;
        while (current is ContentControl { Content: Control content })
        {
            if (content is Panel panel)
            {
                return panel;
            }

            current = content;
        }

        return OverlayLayer.GetOverlayLayer(topLevel);
    }

    private void FullscreenOverlay_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        CloseFullscreenEditor();
        eventArgs.Handled = true;
    }
}
