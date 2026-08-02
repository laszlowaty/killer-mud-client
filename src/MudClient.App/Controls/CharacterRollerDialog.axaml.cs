using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MudClient.Core.Automation;

namespace MudClient.App.Controls;

internal sealed partial class CharacterRollerDialog : Window
{
    public CharacterRollerDialog()
    {
        InitializeComponent();
        foreach (var checkBox in IgnoredCheckBoxes())
        {
            checkBox.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.Property == ToggleButton.IsCheckedProperty)
                {
                    RefreshTargetAvailability();
                }
            };
        }
    }

    private CharacterRollerDialog(
        CharacterRollerConfiguration configuration,
        CharacterRoll? lastRoll)
        : this()
    {
        SetTarget(SumTextBox, SumIgnoredCheckBox, configuration.Sum);
        SetTarget(StrengthTextBox, StrengthIgnoredCheckBox, configuration.Strength);
        SetTarget(IntelligenceTextBox, IntelligenceIgnoredCheckBox, configuration.Intelligence);
        SetTarget(WisdomTextBox, WisdomIgnoredCheckBox, configuration.Wisdom);
        SetTarget(DexterityTextBox, DexterityIgnoredCheckBox, configuration.Dexterity);
        SetTarget(ConstitutionTextBox, ConstitutionIgnoredCheckBox, configuration.Constitution);
        SetTarget(CharismaTextBox, CharismaIgnoredCheckBox, configuration.Charisma);
        FinishCharacterCreationCheckBox.IsChecked = configuration.FinishCharacterCreation;

        if (lastRoll is not null)
        {
            LastRollText.Text =
                $"Ostatni wynik: suma {lastRoll.Sum}, STR {lastRoll.Strength}, INT {lastRoll.Intelligence}, " +
                $"WIS {lastRoll.Wisdom}, DEX {lastRoll.Dexterity}, CON {lastRoll.Constitution}, CHA {lastRoll.Charisma}.";
            LastRollText.IsVisible = true;
        }

        Opened += (_, _) =>
        {
            if (FirstEditableTextBox() is { } textBox)
            {
                textBox.Focus();
            }
            else
            {
                StartButton.Focus();
            }
        };
    }

    internal static Task<CharacterRollerConfiguration?> ShowAsync(
        Window owner,
        CharacterRollerConfiguration configuration,
        CharacterRoll? lastRoll) =>
        new CharacterRollerDialog(configuration, lastRoll)
            .ShowDialog<CharacterRollerConfiguration?>(owner);

    private void RefreshTargetAvailability()
    {
        SetEnabled(SumTextBox, SumIgnoredCheckBox);
        SetEnabled(StrengthTextBox, StrengthIgnoredCheckBox);
        SetEnabled(IntelligenceTextBox, IntelligenceIgnoredCheckBox);
        SetEnabled(WisdomTextBox, WisdomIgnoredCheckBox);
        SetEnabled(DexterityTextBox, DexterityIgnoredCheckBox);
        SetEnabled(ConstitutionTextBox, ConstitutionIgnoredCheckBox);
        SetEnabled(CharismaTextBox, CharismaIgnoredCheckBox);
    }

    private IEnumerable<CheckBox> IgnoredCheckBoxes()
    {
        yield return SumIgnoredCheckBox;
        yield return StrengthIgnoredCheckBox;
        yield return IntelligenceIgnoredCheckBox;
        yield return WisdomIgnoredCheckBox;
        yield return DexterityIgnoredCheckBox;
        yield return ConstitutionIgnoredCheckBox;
        yield return CharismaIgnoredCheckBox;
    }

    private void Start_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (!TryReadTarget("Suma", SumTextBox, SumIgnoredCheckBox, out var sum) ||
            !TryReadTarget("STR", StrengthTextBox, StrengthIgnoredCheckBox, out var strength) ||
            !TryReadTarget("INT", IntelligenceTextBox, IntelligenceIgnoredCheckBox, out var intelligence) ||
            !TryReadTarget("WIS", WisdomTextBox, WisdomIgnoredCheckBox, out var wisdom) ||
            !TryReadTarget("DEX", DexterityTextBox, DexterityIgnoredCheckBox, out var dexterity) ||
            !TryReadTarget("CON", ConstitutionTextBox, ConstitutionIgnoredCheckBox, out var constitution) ||
            !TryReadTarget("CHA", CharismaTextBox, CharismaIgnoredCheckBox, out var charisma))
        {
            return;
        }

        Close(new CharacterRollerConfiguration(
            sum,
            strength,
            intelligence,
            wisdom,
            dexterity,
            constitution,
            charisma,
            FinishCharacterCreationCheckBox.IsChecked == true));
    }

    private bool TryReadTarget(
        string name,
        TextBox textBox,
        CheckBox ignoredCheckBox,
        out int? target)
    {
        target = null;
        if (ignoredCheckBox.IsChecked == true)
        {
            return true;
        }

        if (int.TryParse(
                textBox.Text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value) &&
            value >= 0)
        {
            target = value;
            return true;
        }

        ValidationText.Text = $"{name}: wpisz nieujemną liczbę całkowitą albo zaznacz „Ignorowana”.";
        ValidationText.IsVisible = true;
        textBox.Focus();
        textBox.SelectAll();
        return false;
    }

    private TextBox? FirstEditableTextBox() =>
        new[]
        {
            SumTextBox,
            StrengthTextBox,
            IntelligenceTextBox,
            WisdomTextBox,
            DexterityTextBox,
            ConstitutionTextBox,
            CharismaTextBox,
        }.FirstOrDefault(textBox => textBox.IsEnabled);

    private static void SetTarget(TextBox textBox, CheckBox ignoredCheckBox, int? target)
    {
        textBox.Text = target?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        ignoredCheckBox.IsChecked = target is null;
        SetEnabled(textBox, ignoredCheckBox);
    }

    private static void SetEnabled(TextBox textBox, CheckBox ignoredCheckBox) =>
        textBox.IsEnabled = ignoredCheckBox.IsChecked != true;

    private void Cancel_OnClick(object? sender, RoutedEventArgs eventArgs) => Close(null);
}
