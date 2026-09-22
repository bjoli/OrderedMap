using System;
using System.Collections.Generic;

namespace OrderedMap;

/// <summary>
///     The static interface to <see cref="OrderedMap{TK, TV}" />.
///
///     Every operation takes the map explicitly, and the higher-order ones take the function
///     first and the map last. A fold's callback takes the entry first and the accumulator last,
///     as it does everywhere else in this codebase.
///
///     A callback is handed the key and the value as two arguments — that is what a walk of the
///     tree produces, so nothing is packed and unpacked to make the call.
///
///     Where an entry crosses the boundary as a *value* rather than as an argument list it is a
///     <c>(key, value)</c> tuple rather than a <see cref="KeyValuePair{TK,TV}" />. They hold the
///     same two things, but a tuple is a structural type that any language with tuples already
///     has a name for, which is what lets a caller destructure an entry without knowing anything
///     about this assembly. A partial answer is a tuple for the same reason, with a leading
///     <c>found</c> flag: an <c>out</c> parameter is a C# idiom and nothing else can call it.
/// </summary>
public static class OrderedMapModule
{
    // ---------------------------------------------------------
    // Construction
    // ---------------------------------------------------------

    // orderedmap-empty
    public static OrderedMap<TK, TV> Empty<TK, TV>(IComparer<TK>? comparer = null)
    {
        return OrderedMap<TK, TV>.Empty(comparer);
    }

    // seq->orderedmap
    public static OrderedMap<TK, TV> FromEnumerable<TK, TV>(
        IEnumerable<(TK key, TV value)> source,
        IComparer<TK>? comparer = null)
    {
        var builder = new OrderedMapBuilder<TK, TV>(comparer);
        foreach (var (key, value) in source) builder.Add(key, value);
        return builder.Build();
    }

    /// <summary>The entries, in key order.</summary>
    public static IEnumerable<(TK, TV)> AsEnumerable<TK, TV>(OrderedMap<TK, TV> map)
    {
        foreach (var kvp in map) yield return (kvp.Key, kvp.Value);
    }

    // orderedmap-clear: the empty map under the same ordering
    public static OrderedMap<TK, TV> Clear<TK, TV>(OrderedMap<TK, TV> map)
    {
        return OrderedMap<TK, TV>.Empty(map.Comparer);
    }

    // orderedmap->transientorderedmap
    public static TransientOrderedMap<TK, TV> ToTransient<TK, TV>(OrderedMap<TK, TV> map)
    {
        return map.ToTransient();
    }

    // ---------------------------------------------------------
    // Reading
    // ---------------------------------------------------------

    // orderedmap-length
    public static int Count<TK, TV>(OrderedMap<TK, TV> map)
    {
        return map.Count;
    }

    // orderedmap-empty?
    public static bool IsEmpty<TK, TV>(OrderedMap<TK, TV> map)
    {
        return map.Count == 0;
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

    // orderedmap-ref-or: map key fallback
    public static TV RefOr<TK, TV>(OrderedMap<TK, TV> map, TK key, TV fallback)
    {
        return map.TryGetValue(key, out var value) ? value : fallback;
    }

    // orderedmap-try-ref: map key
    public static (bool found, TV value) TryGetValue<TK, TV>(OrderedMap<TK, TV> map, TK key)
    {
        var found = map.TryGetValue(key, out var value);
        return (found, value);
    }

    // orderedmap-contains?: map key
    public static bool ContainsKey<TK, TV>(OrderedMap<TK, TV> map, TK key)
    {
        return map.ContainsKey(key);
    }

    // orderedmap-keys
    public static IEnumerable<TK> Keys<TK, TV>(OrderedMap<TK, TV> map)
    {
        foreach (var kvp in map) yield return kvp.Key;
    }

    // orderedmap-values
    public static IEnumerable<TV> Values<TK, TV>(OrderedMap<TK, TV> map)
    {
        foreach (var kvp in map) yield return kvp.Value;
    }

    // ---------------------------------------------------------
    // Writing
    // ---------------------------------------------------------

    // orderedmap-set: map key value
    public static OrderedMap<TK, TV> Set<TK, TV>(OrderedMap<TK, TV> map, TK key, TV value)
    {
        return map.Set(key, value);
    }

    // orderedmap-remove: map key
    public static OrderedMap<TK, TV> Remove<TK, TV>(OrderedMap<TK, TV> map, TK key)
    {
        return map.Remove(key);
    }

    /// <summary>
    ///     Every entry of <paramref name="range" />, set in one pass. Through a transient, so the
    ///     tree is copied once rather than once per entry.
    /// </summary>
    public static OrderedMap<TK, TV> SetRange<TK, TV>(
        OrderedMap<TK, TV> map,
        IEnumerable<(TK key, TV value)> range)
    {
        var transient = map.ToTransient();
        foreach (var (key, value) in range) transient.Set(key, value);
        return transient.ToPersistent();
    }

    public static OrderedMap<TK, TV> RemoveRange<TK, TV>(OrderedMap<TK, TV> map, IEnumerable<TK> range)
    {
        var transient = map.ToTransient();
        foreach (var key in range) transient.Remove(key);
        return transient.ToPersistent();
    }

    /// <summary>
    ///     <paramref name="other" /> laid over <paramref name="map" />: where both have a key, the
    ///     right-hand value wins.
    /// </summary>
    public static OrderedMap<TK, TV> Merge<TK, TV>(OrderedMap<TK, TV> map, OrderedMap<TK, TV> other)
    {
        if (map.Count == 0) return other;
        if (other.Count == 0) return map;

        var transient = map.ToTransient();
        foreach (var kvp in other) transient.Set(kvp.Key, kvp.Value);
        return transient.ToPersistent();
    }

    /// <summary>
    ///     The same, with the collisions decided by <paramref name="resolver" />, which is handed
    ///     the key, the left value and the right one.
    /// </summary>
    public static OrderedMap<TK, TV> MergeWith<TK, TV>(
        Func<TK, TV, TV, TV> resolver,
        OrderedMap<TK, TV> map,
        OrderedMap<TK, TV> other)
    {
        var transient = map.ToTransient();
        foreach (var kvp in other)
        {
            transient.Set(
                kvp.Key,
                map.TryGetValue(kvp.Key, out var mine) ? resolver(kvp.Key, mine, kvp.Value) : kvp.Value);
        }
        return transient.ToPersistent();
    }

    // ---------------------------------------------------------
    // Navigation — what the ordering buys over a hashed map
    // ---------------------------------------------------------

    public static (bool found, TK key, TV value) TryGetMin<TK, TV>(OrderedMap<TK, TV> map)
    {
        var found = map.TryGetMin(out var key, out var value);
        return (found, key, value);
    }

    public static (bool found, TK key, TV value) TryGetMax<TK, TV>(OrderedMap<TK, TV> map)
    {
        var found = map.TryGetMax(out var key, out var value);
        return (found, key, value);
    }

    /// <summary>
    ///     The entry whose key is the smallest one greater than <paramref name="key" />, which
    ///     need not itself be in the map.
    /// </summary>
    public static (bool found, TK key, TV value) TryGetSuccessor<TK, TV>(OrderedMap<TK, TV> map, TK key)
    {
        var found = map.TryGetSuccessor(key, out var nextKey, out var nextValue);
        return (found, nextKey, nextValue);
    }

    public static (bool found, TK key, TV value) TryGetPredecessor<TK, TV>(OrderedMap<TK, TV> map, TK key)
    {
        var found = map.TryGetPredecessor(key, out var prevKey, out var prevValue);
        return (found, prevKey, prevValue);
    }

    /// <summary>
    ///     The entries from <paramref name="min" /> to <paramref name="max" />, inclusive. Lazy:
    ///     the tree is descended to <paramref name="min" /> once and then walked, so a slice of a
    ///     large map costs its own size rather than the map's.
    /// </summary>
    public static IEnumerable<(TK, TV)> Range<TK, TV>(OrderedMap<TK, TV> map, TK min, TK max)
    {
        foreach (var kvp in map.Range(min, max)) yield return (kvp.Key, kvp.Value);
    }

    public static IEnumerable<(TK, TV)> From<TK, TV>(OrderedMap<TK, TV> map, TK min)
    {
        foreach (var kvp in map.From(min)) yield return (kvp.Key, kvp.Value);
    }

    public static IEnumerable<(TK, TV)> Until<TK, TV>(OrderedMap<TK, TV> map, TK max)
    {
        foreach (var kvp in map.Until(max)) yield return (kvp.Key, kvp.Value);
    }

    // ---------------------------------------------------------
    // Higher-order — function first, map last
    // ---------------------------------------------------------

    /// <summary>
    ///     A new value for every entry, filed under the key it came from. The mapper is handed the
    ///     whole entry and answers a value: what the result is filed under is the key, so letting
    ///     the mapper move one would make collisions this function has no answer for.
    ///     <see cref="MapEntries{TK1,TV1,TK2,TV2}" /> is the one that may.
    /// </summary>
    public static OrderedMap<TK, TV2> Map<TK, TV, TV2>(
        Func<TK, TV, TV2> mapper,
        OrderedMap<TK, TV> map)
    {
        var builder = new OrderedMapBuilder<TK, TV2>(map.Count, map.Comparer);
        foreach (var kvp in map)
        {
            builder.Add(kvp.Key, mapper(kvp.Key, kvp.Value));
        }
        return builder.Build();
    }

    /// <summary>The functor's map: the value moves, the key rides along.</summary>
    public static OrderedMap<TK, TV2> MapValues<TK, TV, TV2>(
        Func<TV, TV2> mapper,
        OrderedMap<TK, TV> map)
    {
        var builder = new OrderedMapBuilder<TK, TV2>(map.Count, map.Comparer);
        foreach (var kvp in map)
        {
            builder.Add(kvp.Key, mapper(kvp.Value));
        }
        return builder.Build();
    }

    // orderedmap-map-entries: the mapper may move the key, so this is a rebuild
    public static OrderedMap<TK2, TV2> MapEntries<TK1, TV1, TK2, TV2>(
        Func<TK1, TV1, (TK2 key, TV2 value)> mapper,
        OrderedMap<TK1, TV1> map,
        IComparer<TK2>? comparer = null)
    {
        var builder = new OrderedMapBuilder<TK2, TV2>(map.Count, comparer);
        foreach (var kvp in map)
        {
            var (key, value) = mapper(kvp.Key, kvp.Value);
            builder.Add(key, value);
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

    /// <summary>
    ///     Walks until <paramref name="action" /> answers false, and reports whether it ran to the
    ///     end. The early-exit walk the predicates below are written in terms of.
    /// </summary>
    public static bool Iter<TK, TV>(Func<TK, TV, bool> action, OrderedMap<TK, TV> map)
    {
        foreach (var kvp in map)
        {
            if (!action(kvp.Key, kvp.Value)) return false;
        }
        return true;
    }

    public static bool Exists<TK, TV>(Func<TK, TV, bool> predicate, OrderedMap<TK, TV> map)
    {
        return !Iter<TK, TV>((key, value) => !predicate(key, value), map);
    }

    /// <summary>The first entry in key order satisfying <paramref name="predicate" />, if any.</summary>
    public static (bool found, TK key, TV value) TryFind<TK, TV>(
        Func<TK, TV, bool> predicate,
        OrderedMap<TK, TV> map)
    {
        foreach (var kvp in map)
        {
            if (predicate(kvp.Key, kvp.Value))
            {
                return (true, kvp.Key, kvp.Value);
            }
        }

        return (false, default!, default!);
    }

    // ---------------------------------------------------------
    // Walking
    // ---------------------------------------------------------

    public static OrderedMapCursor<TK, TV> Cursor<TK, TV>(OrderedMap<TK, TV> map) =>
        new OrderedMapCursor<TK, TV>(map);

    /// <summary>
    ///     Advances the cursor, and answers whether it ran off the end.
    ///
    ///     The advance happens here rather than in a step of its own: a walk asks "is there
    ///     more?" exactly once per entry, so folding the two together is what lets the whole
    ///     traversal allocate nothing after the cursor itself.
    /// </summary>
    public static bool CursorDone<TK, TV>(OrderedMapCursor<TK, TV> cursor) => !cursor.Enumerator.MoveNext();

    public static (TK, TV) CursorCurrent<TK, TV>(OrderedMapCursor<TK, TV> cursor)
    {
        var current = cursor.Enumerator.Current;
        return (current.Key, current.Value);
    }
}

/// <summary>
///     A position in a walk of an <see cref="OrderedMap{TK, TV}" />, in key order.
///
///     <see cref="BTreeEnumerator{TK, TV}" /> is a struct, which is what keeps a <c>foreach</c>
///     allocation-free — and exactly what makes it useless to a caller that has to *hold* the
///     position, since every copy advances independently. This is that struct in a heap cell:
///     one allocation for the walk, none per entry.
/// </summary>
public sealed class OrderedMapCursor<TK, TV>
{
    public BTreeEnumerator<TK, TV> Enumerator;

    public OrderedMapCursor(OrderedMap<TK, TV> map)
    {
        Enumerator = map.GetEnumerator();
    }
}
