using System.Reflection;

namespace MudClient.App.Tests;

public sealed class AvaloniaTestCollectionConventionTests
{
    [Fact]
    public void EveryAvaloniaTestClass_UsesTheSharedUiCollection()
    {
        var offenders = typeof(AvaloniaTestCollectionConventionTests).Assembly
            .GetTypes()
            .Where(ContainsAvaloniaTest)
            .Where(type => !UsesSharedUiCollection(type))
            .Select(type => type.FullName)
            .Order()
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool ContainsAvaloniaTest(Type type) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(method => method.CustomAttributes)
            .Any(attribute => attribute.AttributeType.Namespace == "Avalonia.Headless.XUnit"
                && attribute.AttributeType.Name is "AvaloniaFactAttribute" or "AvaloniaTheoryAttribute");

    private static bool UsesSharedUiCollection(Type type) =>
        type.CustomAttributes.Any(attribute =>
            attribute.AttributeType == typeof(CollectionAttribute)
            && attribute.ConstructorArguments.Count == 1
            && Equals(attribute.ConstructorArguments[0].Value, AvaloniaUiCollection.Name));
}
