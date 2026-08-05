namespace MudClient.App.ViewModels;

public sealed record ScriptLogEntryViewModel(
    string Time,
    string Source,
    string Level,
    string Message);
