using System.Collections.Concurrent;
using System.Reflection;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;

namespace CustomHUD.Integrations;

// The item a PagedCollectionComponent row stands for, which is usually a provider's own contract
// record. The row type is private to SwiftlyS2, but it keeps its item to render it, so the one
// field typed TItem is found once per row type. A row this cannot read is simply not decorated.
internal static class CollectionItems
{
    private static readonly ConcurrentDictionary<Type, FieldInfo?> Fields = new();

    public static object? Of(IMenuComponent component) => Field(component.GetType())?.GetValue(component);

    private static FieldInfo? Field(Type type) => Fields.GetOrAdd(type, static t =>
    {
        if (!t.IsNested || !t.IsGenericType || t.DeclaringType is not { IsGenericType: true } owner
            || owner.GetGenericTypeDefinition() != typeof(PagedCollectionComponent<>))
            return null;

        var item = t.GetGenericArguments()[0];
        var fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(f => f.FieldType == item).ToArray();

        // An int item would be indistinguishable from the row's own index.
        return fields.Length == 1 ? fields[0] : null;
    });
}
