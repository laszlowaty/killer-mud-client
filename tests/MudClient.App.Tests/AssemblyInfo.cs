using Xunit;

// Avalonia's headless test platform keeps per-thread UI state (Dispatcher, Compositor).
// xUnit runs test collections (i.e. test classes) in parallel by default, so several
// headless sessions initialize the Avalonia platform on different threads at once — which
// intermittently crashes init with "The calling thread cannot access this object because a
// different thread owns it". Serialize the assembly so UI tests never run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MudClient.App.Tests;

// Serialization alone still lets each Avalonia UI test class spin up its own HeadlessUnitTestSession,
// and re-initializing the Avalonia platform in a process whose previous session thread lingers
// throws the same cross-thread error. Grouping every UI test class into one collection gives them a
// single shared session (one platform setup), which removes the re-init race for good. Apply
// [Collection(AvaloniaUiCollection.Name)] to every class using [AvaloniaFact]/[AvaloniaTheory].
[CollectionDefinition(Name)]
public sealed class AvaloniaUiCollection
{
    public const string Name = "Avalonia UI";
}
