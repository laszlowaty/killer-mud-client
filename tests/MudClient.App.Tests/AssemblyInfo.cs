using Xunit;

// Avalonia's headless test platform keeps per-thread UI state (Dispatcher, Compositor).
// xUnit runs test collections (i.e. test classes) in parallel by default, so several
// headless sessions initialize the Avalonia platform on different threads at once — which
// intermittently crashes init with "The calling thread cannot access this object because a
// different thread owns it". Serialize the assembly so UI tests never run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MudClient.App.Tests;

// Keep every Avalonia test in one non-parallel collection as an explicit guard against accidental
// concurrent platform access. Per-test Avalonia isolation still requires each test to close every
// window it opens before its application and compositor are torn down. Apply
// [Collection(AvaloniaUiCollection.Name)] to every class using [AvaloniaFact]/[AvaloniaTheory].
[CollectionDefinition(Name)]
public sealed class AvaloniaUiCollection
{
    public const string Name = "Avalonia UI";
}
