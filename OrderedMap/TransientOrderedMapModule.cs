using System;
using System.Collections.Generic;

namespace OrderedMap;

/// <summary>
///     The static interface to <see cref="TransientOrderedMap{TK, TV}" />.
///
///     Deliberately <see cref="OrderedMapModule" />'s interface with the writes turned inside
///     out: the same names in the same argument order, except that a write mutates in place and
///     answers nothing rather than answering a new map. Reading — including the navigation and
///     the range walks — is identical, because a transient *is* a map; what differs is who owns
///     the nodes, which is what lets an edit skip the copy and what makes
///     <see cref="ToPersistent{TK,TV}" /> O(1).
/// </summary>
public static class TransientOrderedMapModule
{
    // ---------------------------------------------------------
    // Construction
    // ---------------------------------------------------------

    // transientorderedmap-empty
    public static TransientOrderedMap<TK, TV> Empty<TK, TV>(IComparer<TK>? comparer = null)
    {
        return BaseOrderedMap<TK, TV>.CreateTransient(comparer);
    }

    // orderedmap->transientorderedmap
    public static TransientOrderedMap<TK, TV> FromPersistent<TK, TV>(OrderedMap<TK, TV> map)
    {
        return map.ToTransient();
    }

    // transientorderedmap->orderedmap
    public static OrderedMap<TK, TV> ToPersistent<TK, TV>(TransientOrderedMap<TK, TV> map)
    {
        return map.ToPersistent();
    }

    public static TransientOrderedMap<TK, TV> FromEnumerable<TK, TV>(
        IEnumerable<(TK key, TV value)> source,
        IComparer<TK>? comparer = null)
    {
        var transient = BaseOrderedMap<TK, TV>.CreateTransient(comparer);
        foreach (var (key, value) in source) transient.Set(key, value);
        return transient;
    }

    public static IEnumerable<(TK, TV)> AsEnumerable<TK, TV>(TransientOrderedMap<TK, TV> map)
    {
        foreach (var kvp in map) yield return (kvp.Key, kvp.Value);
    }

    // ---------------------------------------------------------
    // Reading
    // ---------------------------------------------------------

    public static int Count<TK, TV>(TransientOrderedMap<TK, TV> map) => map.Count;

    public static bool IsEmpty<TK, TV>(TransientOrderedMap<TK, TV> map) => map.Count == 0;

    // transientorderedmap-ref: map key
    public static TV Ref<TK, TV>(TransientOrderedMap<TK, TV> map, TK key)
    {
        if (map.TryGetValue(key, out var value))
        {
            return value;
        }
        throw new KeyNotFoundException($"Key '{key}' not found in the map.");
    }

    public static TV RefOr<TK, TV>(TransientOrderedMap<TK, TV> map, TK key, TV fallback)
    {
        return map.TryGetValue(key, out var value) ? value : fallback;
    }

    public static (bool found, TV value) TryGetValue<TK, TV>(TransientOrderedMap<TK, TV> map, TK key)
    {
        var found = map.TryGetValue(key, out var value);
        return (found, value);
    }

    public static bool ContainsKey<TK, TV>(TransientOrderedMap<TK, TV> map, TK key)
    {
        return map.ContainsKey(key);
    }

    public static IEnumerable<TK> Keys<TK, TV>(TransientOrderedMap<TK, TV> map)
    {
        foreach (var kvp in map) yield return kvp.Key;
    }

    public static IEnumerable<TV> Values<TK, TV>(TransientOrderedMap<TK, TV> map)
    {
        foreach (var kvp in map) yield return kvp.Value;
    }

    public static (bool found, TK key, TV value) TryGetMin<TK, TV>(TransientOrderedMap<TK, TV> map)
    {
        var found = map.TryGetMin(out var key, out var value);
        return (found, key, value);
    }

    public static (bool found, TK key, TV value) TryGetMax<TK, TV>(TransientOrderedMap<TK, TV> map)
    {
        var found = map.TryGetMax(out var key, out var value);
        return (found, key, value);
    }

    public static (bool found, TK key, TV value) TryGetSuccessor<TK, TV>(TransientOrderedMap<TK, TV> map, TK key)
    {
        var found = map.TryGetSuccessor(key, out var nextKey, out var nextValue);
        return (found, nextKey, nextValue);
    }

    public static (bool found, TK key, TV value) TryGetPredecessor<TK, TV>(TransientOrderedMap<TK, TV> map, TK key)
    {
        var found = map.TryGetPredecessor(key, out var prevKey, out var prevValue);
        return (found, prevKey, prevValue);
    }

    public static IEnumerable<(TK, TV)> Range<TK, TV>(TransientOrderedMap<TK, TV> map, TK min, TK max)
    {
        foreach (var kvp in map.Range(min, max)) yield return (kvp.Key, kvp.Value);
    }

    public static IEnumerable<(TK, TV)> From<TK, TV>(TransientOrderedMap<TK, TV> map, TK min)
    {
        foreach (var kvp in map.From(min)) yield return (kvp.Key, kvp.Value);
    }

    public static IEnumerable<(TK, TV)> Until<TK, TV>(TransientOrderedMap<TK, TV> map, TK max)
    {
        foreach (var kvp in map.Until(max)) yield return (kvp.Key, kvp.Value);
    }

    // ---------------------------------------------------------
    // Writing — in place, answering nothing
    // ---------------------------------------------------------

    // transientorderedmap-set!: map key value
    public static void Set<TK, TV>(TransientOrderedMap<TK, TV> map, TK key, TV value)
    {
        map.Set(key, value);
    }

    // transientorderedmap-remove!: map key
    public static void Remove<TK, TV>(TransientOrderedMap<TK, TV> map, TK key)
    {
        map.Remove(key);
    }

    public static void SetRange<TK, TV>(
        TransientOrderedMap<TK, TV> map,
        IEnumerable<(TK key, TV value)> range)
    {
        foreach (var (key, value) in range) map.Set(key, value);
    }

    public static void RemoveRange<TK, TV>(TransientOrderedMap<TK, TV> map, IEnumerable<TK> range)
    {
        foreach (var key in range) map.Remove(key);
    }

    /// <summary>
    ///     Drops every entry the predicate rejects, in place. The keys to remove are collected
    ///     first: removing during a walk would invalidate the walk.
    /// </summary>
    public static void FilterInPlace<TK, TV>(
        Func<TK, TV, bool> predicate,
        TransientOrderedMap<TK, TV> map)
    {
        var toRemove = new List<TK>();
        foreach (var kvp in map)
        {
            if (!predicate(kvp.Key, kvp.Value))
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            map.Remove(key);
        }
    }

    // ---------------------------------------------------------
    // Higher-order — function first, map last
    // ---------------------------------------------------------

    public static TransientOrderedMap<TK, TV2> Map<TK, TV, TV2>(
        Func<TK, TV, TV2> mapper,
        TransientOrderedMap<TK, TV> map,
        IComparer<TK>? comparer = null)
    {
        var transient = BaseOrderedMap<TK, TV2>.CreateTransient(comparer);
        foreach (var kvp in map)
        {
            transient.Set(kvp.Key, mapper(kvp.Key, kvp.Value));
        }
        return transient;
    }

    public static TransientOrderedMap<TK, TV2> MapValues<TK, TV, TV2>(
        Func<TV, TV2> mapper,
        TransientOrderedMap<TK, TV> map,
        IComparer<TK>? comparer = null)
    {
        var transient = BaseOrderedMap<TK, TV2>.CreateTransient(comparer);
        foreach (var kvp in map)
        {
            transient.Set(kvp.Key, mapper(kvp.Value));
        }
        return transient;
    }

    public static TransientOrderedMap<TK, TV> Filter<TK, TV>(
        Func<TK, TV, bool> predicate,
        TransientOrderedMap<TK, TV> map)
    {
        var transient = BaseOrderedMap<TK, TV>.CreateTransient(map.Comparer);
        foreach (var kvp in map)
        {
            if (predicate(kvp.Key, kvp.Value))
            {
                transient.Set(kvp.Key, kvp.Value);
            }
        }
        return transient;
    }

    public static TState Fold<TK, TV, TState>(
        Func<TK, TV, TState, TState> folder,
        TState state,
        TransientOrderedMap<TK, TV> map)
    {
        var currentState = state;
        foreach (var kvp in map)
        {
            currentState = folder(kvp.Key, kvp.Value, currentState);
        }
        return currentState;
    }

    public static void ForEach<TK, TV>(
        Action<TK, TV> action,
        TransientOrderedMap<TK, TV> map)
    {
        foreach (var kvp in map)
        {
            action(kvp.Key, kvp.Value);
        }
    }

    public static bool Iter<TK, TV>(Func<TK, TV, bool> action, TransientOrderedMap<TK, TV> map)
    {
        foreach (var kvp in map)
        {
            if (!action(kvp.Key, kvp.Value)) return false;
        }
        return true;
    }

    public static bool Exists<TK, TV>(Func<TK, TV, bool> predicate, TransientOrderedMap<TK, TV> map)
    {
        return !Iter<TK, TV>((key, value) => !predicate(key, value), map);
    }

    public static (bool found, TK key, TV value) TryFind<TK, TV>(
        Func<TK, TV, bool> predicate,
        TransientOrderedMap<TK, TV> map)
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

    /// <summary>
    ///     A walk of the map as it stands. Finish it before the next write: a transient is edited
    ///     in place, so a cursor into one is live in a way a persistent map's never is.
    /// </summary>
    public static TransientOrderedMapCursor<TK, TV> Cursor<TK, TV>(TransientOrderedMap<TK, TV> map) =>
        new TransientOrderedMapCursor<TK, TV>(map);

    public static bool CursorDone<TK, TV>(TransientOrderedMapCursor<TK, TV> cursor) =>
        !cursor.Enumerator.MoveNext();

    public static (TK, TV) CursorCurrent<TK, TV>(TransientOrderedMapCursor<TK, TV> cursor)
    {
        var current = cursor.Enumerator.Current;
        return (current.Key, current.Value);
    }
}

/// <summary>
///     A position in a walk of a <see cref="TransientOrderedMap{TK, TV}" />. The struct
///     enumerator in a heap cell, for the reason <see cref="OrderedMapCursor{TK, TV}" /> is.
/// </summary>
public sealed class TransientOrderedMapCursor<TK, TV>
{
    public BTreeEnumerator<TK, TV> Enumerator;

    public TransientOrderedMapCursor(TransientOrderedMap<TK, TV> map)
    {
        Enumerator = map.GetEnumerator();
    }
}
