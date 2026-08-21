using System.Collections.Generic;

namespace OrderedMap;

/// <summary>
///     The static interface to <see cref="OrderedMapBuilder{TK, TV}" />: append, then build once.
///
///     A builder is the bulk path. It collects into a flat list and sorts it into a B-tree in
///     one go, where setting a key on the map itself pays a copy down the tree per pair.
/// </summary>
public static class OrderedMapBuilderModule
{
    // orderedmapbuilder-empty
    public static OrderedMapBuilder<TK, TV> Empty<TK, TV>(IComparer<TK>? comparer = null)
    {
        return new OrderedMapBuilder<TK, TV>(comparer);
    }

    // orderedmapbuilder-empty, with room reserved
    public static OrderedMapBuilder<TK, TV> Empty<TK, TV>(int capacity, IComparer<TK>? comparer = null)
    {
        return new OrderedMapBuilder<TK, TV>(capacity, comparer);
    }

    // orderedmap->orderedmapbuilder
    public static OrderedMapBuilder<TK, TV> FromMap<TK, TV>(OrderedMap<TK, TV> map)
    {
        var builder = new OrderedMapBuilder<TK, TV>(map.Count, map.Comparer);
        foreach (var kvp in map) builder.Add(kvp.Key, kvp.Value);
        return builder;
    }

    // orderedmapbuilder-add!
    public static void Add<TK, TV>(OrderedMapBuilder<TK, TV> builder, TK key, TV value)
    {
        builder.Add(key, value);
    }

    // orderedmapbuilder-add-range!
    //
    // An entry is a `(key, value)` tuple here, as it is everywhere else in this
    // project's static interface: it is the shape any language with tuples can
    // already destructure.
    public static void AddRange<TK, TV>(OrderedMapBuilder<TK, TV> builder, IEnumerable<(TK key, TV value)> range)
    {
        foreach (var (key, value) in range) builder.Add(key, value);
    }

    // orderedmapbuilder-length
    public static int Count<TK, TV>(OrderedMapBuilder<TK, TV> builder)
    {
        return builder.Count;
    }

    // orderedmapbuilder->orderedmap
    public static OrderedMap<TK, TV> Build<TK, TV>(OrderedMapBuilder<TK, TV> builder)
    {
        return builder.Build();
    }
}
