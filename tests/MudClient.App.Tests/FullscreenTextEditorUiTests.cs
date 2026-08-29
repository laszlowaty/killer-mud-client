using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.App.Controls;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views;
using MudClient.App.Views.Panels;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class FullscreenTextEditorUiTests
{
    [AvaloniaFact]
    public void Editor_LimitsInlineHeightAndKeepsScrollingInsideTextBox()
    {
        var editor = new FullscreenTextEditor
        {
            MinHeight = 90,
            TextWrapping = TextWrapping.Wrap,
            Text = "one\r\ntwo\nthree",
        };
        var window = new Window { Width = 500, Height = 700, Content = editor };
        window.Show();

        try
        {
            window.UpdateLayout();
            var textBox = editor.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(textBox => textBox.Name == "InlineEditor");

            Assert.Equal(420, editor.MaxHeight);
            Assert.Equal(
                ScrollBarVisibility.Auto,
                textBox.GetValue(ScrollViewer.VerticalScrollBarVisibilityProperty));
            Assert.Equal(
                ScrollBarVisibility.Disabled,
                textBox.GetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty));
            Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(textBox.Background).Color);
            Assert.Equal(Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(textBox.Foreground).Color);
            var renderedBackground = textBox.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "PART_BorderElement");
            var lineNumberMargin = textBox.GetVisualDescendants()
                .OfType<JavaScriptLineNumberMargin>()
                .Single();
            Assert.Equal(
                Colors.White,
                Assert.IsAssignableFrom<ISolidColorBrush>(renderedBackground.Background).Color);
            Assert.Equal(3, lineNumberMargin.LineCount);
            Assert.NotNull(lineNumberMargin.Presenter);
            Assert.True(lineNumberMargin.Bounds.Width > 0);
            Assert.Contains(
                editor.GetVisualDescendants().OfType<Button>(),
                button => button.Name == "ExpandEditorButton");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FullscreenEditor_UsesWindowOverlayAndSynchronizesText()
    {
        var state = new EditorState { Text = "send('look');" };
        var editor = new FullscreenTextEditor
        {
            EditorTitle = "Kod JavaScript",
            TextWrapping = TextWrapping.NoWrap,
        };
        editor.Bind(
            FullscreenTextEditor.TextProperty,
            new Binding(nameof(EditorState.Text))
            {
                Source = state,
                Mode = BindingMode.TwoWay,
            });
        var widgetLayerManager = new VisualLayerManager
        {
            Width = 220,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            EnableOverlayLayer = true,
            Child = editor,
        };
        var applicationRoot = new Grid();
        applicationRoot.Children.Add(widgetLayerManager);
        var window = new Window { Width = 500, Height = 700, Content = applicationRoot };
        window.Show();

        try
        {
            editor.OpenFullscreenEditor();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.True(editor.IsFullscreen);
            var fullscreenOverlay = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "FullscreenEditorOverlay");
            Assert.Same(applicationRoot, fullscreenOverlay.GetVisualParent());
            Assert.True(fullscreenOverlay.Bounds.Width > widgetLayerManager.Bounds.Width);
            var fullscreenTextBox = window.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(textBox => textBox.Name == "FullscreenEditor");
            Assert.Equal("send('look');", fullscreenTextBox.Text);
            Assert.Equal(
                Colors.White,
                Assert.IsAssignableFrom<ISolidColorBrush>(fullscreenTextBox.Background).Color);
            Assert.Equal(
                Colors.Black,
                Assert.IsAssignableFrom<ISolidColorBrush>(fullscreenTextBox.Foreground).Color);
            Assert.Equal(
                ScrollBarVisibility.Auto,
                fullscreenTextBox.GetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty));

            fullscreenTextBox.Text = "send('north');";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("send('north');", editor.Text);
            Assert.Equal("send('north');", state.Text);
            Assert.True(FullscreenTextEditor.TryCloseOpenEditor());
            Assert.False(editor.IsFullscreen);
            Assert.Equal(
                "send('north');",
                editor.GetVisualDescendants()
                    .OfType<TextBox>()
                    .Single(textBox => textBox.Name == "InlineEditor")
                    .Text);
        }
        finally
        {
            FullscreenTextEditor.TryCloseOpenEditor();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void JavaScriptMode_RemainsEditableAndReportsParserErrors()
    {
        var editor = new FullscreenTextEditor
        {
            EditorTitle = "Kod testowy",
            IsJavaScript = true,
            Text = "if (event.data.hp < 25) execute('heal');",
            MinHeight = 160,
        };
        var window = new Window { Width = 600, Height = 500, Content = editor };
        window.Show();

        try
        {
            Pump(window);
            var textEditor = editor.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(control => control.Name == "InlineEditor");
            var validationMessage = editor.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(control => control.Name == "JavaScriptValidationMessage");
            var presenter = textEditor.GetVisualDescendants()
                .OfType<JavaScriptTextPresenter>()
                .Single();

            Assert.True(textEditor.IsEffectivelyVisible);
            Assert.False(textEditor.IsReadOnly);
            Assert.IsType<JavaScriptTextBox>(textEditor);
            Assert.True(presenter.IsSyntaxHighlightingEnabled);
            var keywordRun = presenter.TextLayout.TextLines
                .SelectMany(line => line.TextRuns)
                .Single(run => run.Text.ToString() == "if");
            Assert.Equal(
                Color.Parse("#0000CC"),
                Assert.IsAssignableFrom<ISolidColorBrush>(keywordRun.Properties?.ForegroundBrush).Color);
            Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(textEditor.Background).Color);
            Assert.Equal(Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(textEditor.Foreground).Color);
            Assert.Null(editor.JavaScriptValidationError);
            Assert.Equal("Składnia JavaScript poprawna", validationMessage.Text);

            Assert.DoesNotContain(
                editor.GetVisualDescendants().OfType<Button>(),
                control => control.Name == "SyntaxPreviewButton");
            var textBeforeInput = textEditor.Text;
            textEditor.CaretIndex = textEditor.Text?.Length ?? 0;
            ClickAndType(window, textEditor, " ");
            Assert.NotEqual(textBeforeInput, textEditor.Text);
            Assert.Equal(textEditor.Text, editor.Text);

            textEditor.Text = "if (";
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(editor.JavaScriptValidationError);
            Assert.Contains("Kod testowy", editor.JavaScriptValidationError);
            Assert.Equal(editor.JavaScriptValidationError, validationMessage.Text);

            editor.IsJavaScript = false;
            Dispatcher.UIThread.RunJobs();

            Assert.Null(editor.JavaScriptValidationError);
            Assert.False(validationMessage.IsVisible);
            Assert.True(textEditor.IsVisible);
            Assert.False(presenter.IsSyntaxHighlightingEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LargeJavaScript_DisablesLiveAnalysisAndDefersHiddenEditorSynchronization()
    {
        var originalText = new string('x', FullscreenTextEditor.LiveJavaScriptFeaturesMaximumLength + 1);
        var editor = new FullscreenTextEditor
        {
            IsJavaScript = true,
            Text = originalText,
        };
        var applicationRoot = new Grid { Children = { editor } };
        var window = new Window { Width = 700, Height = 600, Content = applicationRoot };
        window.Show();

        try
        {
            Pump(window);
            var inlineEditor = editor.GetVisualDescendants()
                .OfType<JavaScriptTextBox>()
                .Single(control => control.Name == "InlineEditor");
            var inlinePresenter = inlineEditor.GetVisualDescendants()
                .OfType<JavaScriptTextPresenter>()
                .Single();
            var lineNumbers = inlineEditor.GetVisualDescendants()
                .OfType<JavaScriptLineNumberMargin>()
                .Single();
            var validationMessage = editor.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(control => control.Name == "JavaScriptValidationMessage");

            Assert.True(editor.IsLargeJavaScriptDocument);
            Assert.False(inlinePresenter.IsSyntaxHighlightingEnabled);
            Assert.False(lineNumbers.IsVisible);
            Assert.Contains("walidacja na żywo", validationMessage.Text);

            editor.OpenFullscreenEditor();
            Pump(window);
            var fullscreenEditor = window.GetVisualDescendants()
                .OfType<JavaScriptTextBox>()
                .Single(control => control.Name == "FullscreenEditor");
            fullscreenEditor.Text = originalText + "y";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(originalText + "y", editor.Text);
            Assert.Equal(originalText, inlineEditor.Text);

            editor.CloseFullscreenEditor();
            Assert.Equal(originalText + "y", inlineEditor.Text);
        }
        finally
        {
            FullscreenTextEditor.TryCloseOpenEditor();
            window.Close();
        }
    }

    [Fact]
    public void JavaScriptHighlighter_FindsBasicTokensWithoutChangingTheText()
    {
        const string code = "const hp = 25; // heal\nsend(\"north\");";

        var tokens = JavaScriptSyntaxHighlighter.GetSpans(code)
            .Select(span => (Text: code.Substring(span.Start, span.Length), span.Kind))
            .ToArray();

        Assert.Contains(("const", JavaScriptSyntaxKind.Keyword), tokens);
        Assert.Contains(("25", JavaScriptSyntaxKind.Number), tokens);
        Assert.Contains(("// heal", JavaScriptSyntaxKind.Comment), tokens);
        Assert.Contains(("\"north\"", JavaScriptSyntaxKind.String), tokens);
        Assert.DoesNotContain(tokens, token => token.Text == "send");
    }

    [Fact]
    public void LineNumberMargin_RecognizesWindowsAndUnixLineEndings()
    {
        Assert.Equal(
            new[] { 0, 3, 5, 7 },
            JavaScriptLineNumberMargin.GetLogicalLineStarts("a\r\nb\nc\r"));
    }

    [AvaloniaFact]
    public async Task FullscreenEditor_InMainWindow_CoversApplicationWindowInsteadOfAutomationWidget()
    {
        var directory = Directory.CreateTempSubdirectory("fullscreen-editor-tests-").FullName;
        var viewModel = new MainWindowViewModel(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory));
        var window = new MainWindow
        {
            DataContext = viewModel,
            Width = 1400,
            Height = 900,
        };

        try
        {
            viewModel.SelectedAutomationTabIndex = 0;
            viewModel.StartAddTimerCommand.Execute(null);
            viewModel.NewTimerIsAdvanced = true;
            window.Show();
            Pump(window);

            var editor = window.GetVisualDescendants()
                .OfType<FullscreenTextEditor>()
                .Single(control => control.IsEffectivelyVisible);
            Assert.True(editor.IsJavaScript);
            Assert.Contains(
                editor.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "InlineEditor" &&
                           control is TextBox &&
                           control.IsEffectivelyVisible);
            var inlineTextEditor = editor.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(control => control.Name == "InlineEditor" && control.IsEffectivelyVisible);
            FocusAndType(window, inlineTextEditor, "i");
            Assert.Contains("i", viewModel.NewTimerCommands);
            Assert.True(editor.Bounds.Width < window.ClientSize.Width / 2);

            var expandButton = editor.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name == "ExpandEditorButton");
            expandButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump(window);

            var overlay = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "FullscreenEditorOverlay");
            Assert.Contains(
                window.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "FullscreenEditor" &&
                           control is TextBox &&
                           control.IsEffectivelyVisible);
            var fullscreenTextEditor = window.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(control => control.Name == "FullscreenEditor" && control.IsEffectivelyVisible);
            fullscreenTextEditor.CaretIndex = fullscreenTextEditor.Text?.Length ?? 0;
            ClickAndType(window, fullscreenTextEditor, "f");
            Assert.Contains("f", viewModel.NewTimerCommands);
            Assert.True(
                overlay.Bounds.Width >= window.ClientSize.Width - 1,
                $"Overlay width {overlay.Bounds.Width:F1} did not cover window width {window.ClientSize.Width:F1}.");
            Assert.True(
                overlay.Bounds.Height >= window.ClientSize.Height - 1,
                $"Overlay height {overlay.Bounds.Height:F1} did not cover window height {window.ClientSize.Height:F1}.");
        }
        finally
        {
            FullscreenTextEditor.TryCloseOpenEditor();
            await window.CloseAndDisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task AutomationForms_KeepAdvancedRulesTimersAndScriptsEditable()
    {
        var directory = Directory.CreateTempSubdirectory("automation-js-editor-tests-").FullName;
        var viewModel = new MainWindowViewModel(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory));
        var panel = new AutomationPanelView { DataContext = viewModel };
        var window = new Window { Width = 700, Height = 800, Content = panel };
        window.Show();

        try
        {
            viewModel.SelectedAutomationTabIndex = 0;
            viewModel.StartAddTimerCommand.Execute(null);
            viewModel.NewTimerIsAdvanced = true;
            Pump(window);
            var timerEditor = VisibleAutomationEditor(window);
            var timerCodeEditor = VisibleTextEditor(timerEditor);
            ClickAndType(window, timerCodeEditor, "t");
            Assert.Equal(timerCodeEditor.Text, viewModel.NewTimerCommands);
            Assert.Contains("t", viewModel.NewTimerCommands);

            viewModel.CancelTimerEditCommand.Execute(null);
            viewModel.SelectedAutomationTabIndex = 1;
            viewModel.StartAddAliasCommand.Execute(null);
            Pump(window);
            var aliasEditor = VisibleAutomationEditor(window);
            Assert.False(aliasEditor.IsJavaScript);

            viewModel.NewRuleIsAdvanced = true;
            Pump(window);
            Assert.True(aliasEditor.IsJavaScript);
            var aliasCodeEditor = VisibleTextEditor(aliasEditor);
            ClickAndType(window, aliasCodeEditor, "x");
            Assert.Equal(aliasCodeEditor.Text, viewModel.NewRuleAction);
            Assert.Contains("x", viewModel.NewRuleAction);

            viewModel.CancelRuleEditCommand.Execute(null);
            viewModel.SelectedAutomationTabIndex = 2;
            viewModel.StartAddTriggerCommand.Execute(null);
            Pump(window);
            var triggerEditor = VisibleAutomationEditor(window);
            Assert.False(triggerEditor.IsJavaScript);

            viewModel.NewRuleIsAdvanced = true;
            Pump(window);
            Assert.True(triggerEditor.IsJavaScript);
            var triggerCodeEditor = VisibleTextEditor(triggerEditor);
            ClickAndType(window, triggerCodeEditor, "r");
            Assert.Equal(triggerCodeEditor.Text, viewModel.NewRuleAction);
            Assert.Contains("r", viewModel.NewRuleAction);

            viewModel.CancelRuleEditCommand.Execute(null);
            viewModel.SelectedAutomationTabIndex = 3;
            viewModel.StartAddScriptCommand.Execute(null);
            Pump(window);
            var scriptEditor = VisibleAutomationEditor(window);
            Assert.True(scriptEditor.IsJavaScript);
            var scriptCodeEditor = VisibleTextEditor(scriptEditor);
            ClickAndType(window, scriptCodeEditor, "s");
            Assert.Equal(scriptCodeEditor.Text, viewModel.NewScriptCode);
            Assert.Contains("s", viewModel.NewScriptCode);
        }
        finally
        {
            window.Close();
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FullscreenTextEditor VisibleAutomationEditor(Window window) =>
        window.GetVisualDescendants()
            .OfType<FullscreenTextEditor>()
            .Single(control => control.IsEffectivelyVisible);

    private static TextBox VisibleTextEditor(FullscreenTextEditor editor) =>
        editor.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(control => control.Name == "InlineEditor" && control.IsEffectivelyVisible);

    private static void ClickAndType(Window window, TextBox editor, string text)
    {
        editor.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var transform = editor.TransformToVisual(window);
        Assert.NotNull(transform);
        var clickPoint = transform!.Value.Transform(new Point(48, Math.Min(16, editor.Bounds.Height / 2)));
        window.MouseDown(clickPoint, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(clickPoint, MouseButton.Left, RawInputModifiers.None);
        Assert.True(
            editor.IsFocused,
            $"TextBox did not receive focus. Bounds: {editor.Bounds}, point: {clickPoint}.");
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    private static void FocusAndType(Window window, TextBox editor, string text)
    {
        Assert.True(editor.Focus());
        Assert.True(editor.IsFocused);
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Pump(Window window, int iterations = 12)
    {
        for (var index = 0; index < iterations; index++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class EditorState : INotifyPropertyChanged
    {
        private string? _text;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Text
        {
            get => _text;
            set
            {
                if (_text == value)
                {
                    return;
                }

                _text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }
    }
}
