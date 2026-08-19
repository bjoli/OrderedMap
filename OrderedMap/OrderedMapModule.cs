using System;
using System.Collections.Generic;

namespace OrderedMap;

public static class OrderedMapModule
{
    // orderedmap-map: function map
    public static OrderedMap<TK2, TV2> Map<TK1, TV1, TK2, TV2>(
        Func<TK1, TV1, KeyValuePair<TK2, TV2>> mapper,
        OrderedMap<TK1, TV1> map,
        IComparer<TK2>? comparer = null)
    {
        var builder = new OrderedMapBuilder<TK2, TV2>(map.Count, comparer);
        foreach (var kvp in map)
        {
            var mapped = mapper(kvp.Key, kvp.Value);
            builder.Add(mapped.Key, mapped.Value);
        }
        return builder.Build();
    }

    // orderedmap-filter: predicate map
    public static OrderedMap<TK, TV> Filter<TK, TV>(
        Func<TK, TV, bool> predicate,
        OrderedMap<TK, TV> map)
    {
        var builder = new OrderedMapBuilder<TK, TV>(map.Count, map.Comparer);
        foreach (var kvp in map)
        {
            if (predicate(kvp.Key, kvp.Value))
            {
                builder.Add(kvp.Key, kvp.Value);
            }
        }
        return builder.Build();
    }

    // orderedmap-fold: folder state map
    public static TState Fold<TK, TV, TState>(
        Func<TK, TV, TState, TState> folder,
        TState state,
        OrderedMap<TK, TV> map)
    {
        var currentState = state;
        foreach (var kvp in map)
        {
            currentState = folder(kvp.Key, kvp.Value, currentState);
        }
        return currentState;
    }

    // orderedmap-for-each: action map
    public static void ForEach<TK, TV>(
        Action<TK, TV> action,
        OrderedMap<TK, TV> map)
    {
        foreach (var kvp in map)
        {
            action(kvp.Key, kvp.Value);
        }
    }

    // orderedmap-ref: map key
    public static TV Ref<TK, TV>(OrderedMap<TK, TV> map, TK key)
    {
        if (map.TryGetValue(key, out var value))
        {
            return value;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in the map.");
    }
    
    // orderedmap-set: map key value
    public static OrderedMap<TK, TV> Set<TK, TV>(OrderedMap<TK, TV> map, TK key, TV value)
    {
        return map.Set(key, value);
    }

    // orderedmap-delete: map key
    public static OrderedMap<TK, TV> Remove<TK, TV>(OrderedMap<TK, TV> map, TK key)
    {
        return map.Remove(key);
    }

    // orderedmap-contains?: map key
    public static bool ContainsKey<TK, TV>(OrderedMap<TK, TV> map, TK key)
    {
        return map.ContainsKey(key);
    }

    // orderedmap-empty: 
    public static OrderedMap<TK, TV> Empty<TK, TV>(IComparer<TK>? comparer = null)
    {
        return OrderedMap<TK, TV>.Empty(comparer);
    }
}
