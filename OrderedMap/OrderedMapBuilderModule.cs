using System;
using System.Collections.Generic;

namespace OrderedMap;

public static class OrderedMapBuilderModule
{
    // orderedmap-builder
    public static OrderedMapBuilder<TK, TV> Empty<TK, TV>(IComparer<TK>? comparer = null)
    {
        return new OrderedMapBuilder<TK, TV>(comparer);
    }

    // orderedmap-builder
    public static OrderedMapBuilder<TK, TV> Empty<TK, TV>(int capacity, IComparer<TK>? comparer = null)
    {
        return new OrderedMapBuilder<TK, TV>(capacity, comparer);
    }

    // orderedmap-builder-add!
    public static OrderedMapBuilder<TK, TV> Add<TK, TV>(OrderedMapBuilder<TK, TV> builder, TK key, TV value)
    {
        builder.Add(key, value);
        return builder;
    }

    // orderedmap-builder->orderedmap
    public static OrderedMap<TK, TV> Build<TK, TV>(OrderedMapBuilder<TK, TV> builder)
    {
        return builder.Build();
    }
}
