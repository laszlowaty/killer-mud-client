using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MudClient.App.Controls;
using MudClient.App.ViewModels;
using MudClient.App.Views;
using Xunit;

namespace MudClient.App.Tests;

/// <summary>
/// Focused tests for the click-to-focus-and-select-all behavior on
/// MainWindow (coder change: click MUD output → redirect focus to
/// command box + select all).
/// </summary>
public sealed class MainWindowClickTests
{
    // ==================================================================
    // Reflection helpers for private members on MainWindow
    // ==================================================================

    private static FieldInfo GetCommandBoxField()
    {
        var field = typeof(MainWindow).GetField("_commandBox",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return field!;
    }

    private static FieldInfo GetHistoryIndexField()
    {
        var field = typeof(MainWindow).GetField("_historyIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return field!;
    }

    private static MethodInfo GetFocusCommandBoxAndSelectAllMethod()
    {
        var method = typeof(MainWindow).GetMethod("FocusCommandBoxAndSelectAll",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        return method!;
    }

    private static MethodInfo GetHandlePostSendMethod()
    {
        var method = typeof(MainWindow).GetMethod("HandlePostSend",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        return method!;
    }

    // ==================================================================
    // FocusCommandBoxAndSelectAll (the core helper)
    // ==================================================================

    [AvaloniaFact]
    public void FocusCommandBoxAndSelectAll_FocusesAndSelectsAll()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        var commandBox = (TextBox)GetCommandBoxField().GetValue(window)!;
        commandBox.Text = "hello world";

        // Act
        GetFocusCommandBoxAndSelectAllMethod().Invoke(window, null);

        // Assert
        Assert.True(commandBox.IsFocused);
        Assert.Equal(0, commandBox.SelectionStart);
        Assert.Equal("hello world".Length, commandBox.SelectionEnd);
    }

    [AvaloniaFact]
    public void FocusCommandBoxAndSelectAll_WithEmptyText_SelectsNothing()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        var commandBox = (TextBox)GetCommandBoxField().GetValue(window)!;
        commandBox.Text = string.Empty;

        // Act
        GetFocusCommandBoxAndSelectAllMethod().Invoke(window, null);

        // Assert
        Assert.True(commandBox.IsFocused);
        // SelectAll on an empty TextBox gives SelectionStart == 0, SelectionEnd == 0
        Assert.Equal(0, commandBox.SelectionStart);
        Assert.Equal(0, commandBox.SelectionEnd);
    }

    // ==================================================================
    // HandlePostSend (focus + select all + history index reset)
    // ==================================================================

    [AvaloniaFact]
    public void HandlePostSend_FocusesAndSelectsAllAndResetsHistoryIndex()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        var commandBox = (TextBox)GetCommandBoxField().GetValue(window)!;
        commandBox.Text = "look north";

        // Simulate being in the middle of history navigation.
        GetHistoryIndexField().SetValue(window, 3);
        Assert.Equal(3, GetHistoryIndexField().GetValue(window));

        // Act
        GetHandlePostSendMethod().Invoke(window, null);

        // Assert
        Assert.True(commandBox.IsFocused);
        Assert.Equal(0, commandBox.SelectionStart);
        Assert.Equal("look north".Length, commandBox.SelectionEnd);
        Assert.Equal(-1, GetHistoryIndexField().GetValue(window));
    }

    [AvaloniaFact]
    public void HandlePostSend_WithEmptyText_ResetsHistoryIndex()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        var commandBox = (TextBox)GetCommandBoxField().GetValue(window)!;
        commandBox.Text = string.Empty;
        GetHistoryIndexField().SetValue(window, 1);

        // Act
        GetHandlePostSendMethod().Invoke(window, null);

        // Assert
        Assert.True(commandBox.IsFocused);
        Assert.Equal(-1, GetHistoryIndexField().GetValue(window));
    }

    // ==================================================================
    // Window_OnPointerPressed — redirect to command box
    // ==================================================================

    /// <summary>
    /// Helper: fully lay out the window so Bounds and TranslatePoint work.
    /// </summary>
    private static void EnsureLayout(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void PointerPressed_OnNonInteractiveArea_FocusesCommandBoxAndSelectsAll()
    {
        // Verifies the end-to-end behavior: a pointer press on a
        // non-excluded area (here: the border behind the MudOutputView)
        // redirects focus to the command box.
        //
        // We use MouseDown at (0, 0) which lands on the window chrome
        // (a non-excluded area) to avoid layout-coordinate fragility.
        // The logic-based tests above already prove that MudOutputView
        // children are not excluded via FindAncestorOfType<Button> etc.
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        EnsureLayout(window);

        var commandBox = (TextBox)GetCommandBoxField().GetValue(window)!;
        commandBox.Text = "select-all-test";

        // Act: click at the top-left corner of the window (hits the
        // window/parent area, not an excluded control).
        window.MouseDown(new Point(1, 1), MouseButton.Left);
        window.MouseUp(new Point(1, 1), MouseButton.Left);

        // Assert: the handler redirected focus and selected all text.
        Assert.True(commandBox.IsFocused,
            "CommandBox should be focused after pointer press on non-excluded area.");
        Assert.Equal(0, commandBox.SelectionStart);
        Assert.Equal("select-all-test".Length, commandBox.SelectionEnd);
    }

    [AvaloniaFact]
    public void PointerPressed_OnButton_FindAncestorOfType_ExcludesButton()
    {
        // This test validates the exclusion logic directly rather than
        // relying on coordinate-based hit-testing, which is fragile in
        // headless mode without guaranteed layout.
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        EnsureLayout(window);

        var commandBox = (TextBox)GetCommandBoxField().GetValue(window)!;
        commandBox.Text = "do not select";

        // Pick a visible Button with non-empty bounds.
        var anyButton = window
            .GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Bounds.Width > 0 && b.Bounds.Height > 0);
        Assert.NotNull(anyButton);
        Assert.True(anyButton.Bounds.Width > 0,
            "Chosen button must have non-zero width.");

        // The Button's visual content (e.g. TextBlock or Border) would be
        // the Source in a real pointer event.  Use the button itself since
        // FindAncestorOfType<Button>(includeSelf: true) includes self.
        var source = anyButton;

        // Act: simulate what Window_OnPointerPressed does — check
        // FindAncestorOfType<Button> on this source.
        var ancestorButton = source.FindAncestorOfType<Button>(includeSelf: true);

        // Assert: the source IS a Button (or inside one), so the exclusion
        // check returns non-null and the handler would return early.
        Assert.NotNull(ancestorButton);
        Assert.Same(anyButton, ancestorButton);
    }

    [AvaloniaFact]
    public void PointerPressed_OnMudOutput_FindAncestorOfType_DoesNotMatchButton()
    {
        // Validates that clicking on MudOutputView does NOT find a Button
        // ancestor, so the handler proceeds to FocusCommandBoxAndSelectAll.
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        EnsureLayout(window);

        var mudOutput = window.FindControl<MudOutputView>("MudOutput");
        Assert.NotNull(mudOutput);

        // The MudOutputView's first visual child at the top-left is likely
        // a Border (its outermost element in the XAML template).
        var firstChild = mudOutput.GetVisualDescendants().FirstOrDefault();
        Assert.NotNull(firstChild);
        var visual = firstChild as Visual ?? mudOutput;

        // Act / Assert: no Button ancestor is found.
        Assert.Null(visual.FindAncestorOfType<Button>(includeSelf: true));
    }

    [AvaloniaFact]
    public void PointerPressed_OnSendButton_FocusesCommandBoxViaHandler()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        EnsureLayout(window);

        var commandBox = (TextBox)GetCommandBoxField().GetValue(window)!;
        commandBox.Text = "look";

        // The Send button in XAML has no x:Name, so find it by its
        // content text.
        var sendButton = window
            .GetLogicalDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Content is string s && s.Contains("Wyślij"));
        Assert.NotNull(sendButton);

        // Act: raise the Click event as the UI would after mouse-up.
        // The pointer-pressed handler will skip it (Button exclusion),
        // but the Click event fires separately and calls
        // SendButton_OnClick → HandlePostSend().
        sendButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // Assert: command box is focused and text is selected.
        Assert.True(commandBox.IsFocused);
        Assert.Equal(0, commandBox.SelectionStart);
        Assert.Equal("look".Length, commandBox.SelectionEnd);
        Assert.Equal(-1, GetHistoryIndexField().GetValue(window));
    }

    // ==================================================================
    // SelectableTextBlock inside MudOutputView → redirect
    // ==================================================================

    /// <summary>
    /// End-to-end: clicking a SelectableTextBlock inside MudOutputView
    /// goes through Window_OnPointerPressed and redirects focus to
    /// the command box with all text selected.
    /// Uses real MouseDown (headless hit-testing) — no reflection.
    /// </summary>
    [AvaloniaFact]
    public void PointerPressed_OnMudOutputSelectableTextBlock_RedirectsToCommandBox()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        var mudOutput = window.FindControl<MudOutputView>("MudOutput");
        Assert.NotNull(mudOutput);
        mudOutput.AppendText("clickable output line\n");

        var commandBox = window.FindControl<TextBox>("CommandBox");
        Assert.NotNull(commandBox);
        commandBox.Text = "existing command";

        EnsureLayout(window);

        // Find the output pane inside MudOutput with non-zero bounds.
        var mudLine = mudOutput
            .GetVisualDescendants()
            .OfType<OutputPaneControl>()
            .FirstOrDefault(s => s.Bounds.Width > 0 && s.Bounds.Height > 0);
        Assert.NotNull(mudLine);
        Assert.True(mudLine.Bounds.Width > 0,
            "OutputPaneControl inside MudOutput must have non-zero width after layout.");

        // Click near the pane's top-left corner (where the output text lives) instead of
        // its center: the pane spans the whole output area, and the window's center is
        // covered by the profile-selection overlay when no profile is active yet.
        var relativePoint = new Point(10, 10);
        var windowPoint = mudLine.TranslatePoint(relativePoint, window);
        Assert.NotNull(windowPoint);

        // Act: simulate a click at the SelectableTextBlock's center.
        window.MouseDown(windowPoint.Value, MouseButton.Left);
        window.MouseUp(windowPoint.Value, MouseButton.Left);

        // Assert: command box receives focus and full text selection.
        Assert.True(commandBox.IsFocused,
            "CommandBox must be focused after clicking MUD output text.");
        Assert.Equal(0, commandBox.SelectionStart);
        Assert.Equal("existing command".Length, commandBox.SelectionEnd);
    }

    // ==================================================================
    // SelectableTextBlock outside MudOutputView → excluded
    // ==================================================================

    /// <summary>
    /// Clicking a SelectableTextBlock that is NOT inside MudOutputView
    /// must NOT redirect focus to the command box. Verifies the fix for
    /// the reviewer's MEDIUM finding (regression of non-output
    /// selectable text areas).
    ///
    /// Uses RaiseEvent with a PointerPressedEventArgs constructed
    /// directly on the non-output SelectableTextBlock, bypassing
    /// headless hit-testing which does not reliably resolve
    /// programmatically-added SelectableTextBlock instances.
    /// The event bubbles up to Window_OnPointerPressed with Source
    /// set to the non-output block — exercise the real handler logic.
    /// </summary>
    [AvaloniaFact]
    public void PointerPressed_OnNonOutputSelectableTextBlock_IsExcluded()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        // Add a SelectableTextBlock to the window's visual tree
        // outside MudOutputView.
        var rootGrid = Assert.IsAssignableFrom<Grid>(window.Content);
        var nonOutputBlock = new SelectableTextBlock
        {
            Text = "non-output excluded text",
            Height = 20,
        };
        Grid.SetRow(nonOutputBlock, 2);
        rootGrid.Children.Add(nonOutputBlock);

        var commandBox = window.FindControl<TextBox>("CommandBox");
        Assert.NotNull(commandBox);
        commandBox.Text = "do not select";

        EnsureLayout(window);

        // Act: construct PointerPressedEventArgs whose Source will be
        // the non-output SelectableTextBlock, then raise it on the
        // block so the event bubbles up to Window_OnPointerPressed.
        var avaloniaPointer = new Avalonia.Input.Pointer(
            Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var props = new PointerPointProperties();
        var pointerArgs = new PointerPressedEventArgs(
            nonOutputBlock,
            avaloniaPointer,
            window,
            new Point(0, 0),
            0UL,
            props,
            KeyModifiers.None,
            clickCount: 1);

        nonOutputBlock.RaiseEvent(pointerArgs);

        // Assert: command box must NOT be focused (exclusion should
        // have prevented the redirect).
        Assert.False(commandBox.IsFocused,
            "CommandBox must NOT be focused after clicking non-output SelectableTextBlock.");
    }

    // ==================================================================
    // Direct FindAncestorOfType logic tests (no event simulation)
    // ==================================================================

    /// <summary>
    /// A SelectableTextBlock inside MudOutputView correctly finds its
    /// MudOutputView ancestor — this is the condition that enables the
    /// click-to-focus redirect for MUD output text.
    /// </summary>
    [AvaloniaFact]
    public void PointerPressed_MudOutputSelectableTextBlock_FindsMudOutputAncestor()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        EnsureLayout(window);

        var mudOutput = window.FindControl<MudOutputView>("MudOutput");
        Assert.NotNull(mudOutput);
        mudOutput.AppendText("test line\n");
        EnsureLayout(window);

        var mudLine = mudOutput
            .GetVisualDescendants()
            .OfType<OutputPaneControl>()
            .FirstOrDefault(s => s.Bounds.Width > 0);
        Assert.NotNull(mudLine);

        // Act: the output pane must be inside MudOutputView, so clicks on it
        // are not excluded from the command-box focus redirect.
        var mudOutputAncestor = mudLine.FindAncestorOfType<MudOutputView>(includeSelf: true);

        // Assert
        Assert.NotNull(mudOutputAncestor);
        Assert.Same(mudOutput, mudOutputAncestor);
    }

    /// <summary>
    /// A SelectableTextBlock that is NOT inside MudOutputView does NOT
    /// find a MudOutputView ancestor — this is the exclusion condition
    /// that prevents redirect for non-output selectable text areas.
    /// </summary>
    [AvaloniaFact]
    public void PointerPressed_NonOutputSelectableTextBlock_FindsNoMudOutputAncestor()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        var rootGrid = Assert.IsAssignableFrom<Grid>(window.Content);
        var nonOutputBlock = new SelectableTextBlock
        {
            Text = "non-output",
            Height = 20,
        };
        Grid.SetRow(nonOutputBlock, 2);
        rootGrid.Children.Add(nonOutputBlock);
        EnsureLayout(window);

        // Act: simulate what Window_OnPointerPressed does.
        var selectable = nonOutputBlock.FindAncestorOfType<SelectableTextBlock>(includeSelf: true);
        Assert.NotNull(selectable);
        var mudOutputAncestor = selectable.FindAncestorOfType<MudOutputView>(includeSelf: true);

        // Assert: SelectableTextBlock is found (self), but no
        // MudOutputView ancestor — handler returns early (excluded).
        Assert.Same(nonOutputBlock, selectable);
        Assert.Null(mudOutputAncestor);
    }
}
