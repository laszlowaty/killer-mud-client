using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// A named, per-profile collection of buffs. Only the selected set is shown
/// and used by /recast without an argument, while every set keeps its live GMCP status
/// and can be recast directly with /recast &lt;set name&gt;.
/// </summary>
public sealed partial class BuffSetEntry : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<BuffWatchEntry> Buffs { get; } = [];
}
