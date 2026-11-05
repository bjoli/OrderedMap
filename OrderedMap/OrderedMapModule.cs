using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
///     about this assembly.
///
///     A partial answer is <c>bool TryXyz(..., out ...)</c>, the outs marked
///     <c>[MaybeNullWhen(false)]</c>. Bjolang imports it with <c>(out T)</c> and gets an
///     <c>Option</c>, so an out that is left as <c>default</c> on false is never read.
/// </summary>
[NonUserCode]
public static class OrderedMapModule
{
    // ---------------------------------------------------------
    // Construction
    // ---------------------------------------------------------

    /// <summary>What the map orders its keys by.</summary>
    public static IComparer<TK> Comparer<TK, TV>(OrderedMap<TK, TV> map) => map.Comparer;

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
    public static bool TryGetValue<TK, TV>(OrderedMap<TK, TV> map, TK key, [MaybeNullWhen(false)] out TV value) =>
        map.TryGetValue(key, out value);

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

    /// <summary>(std parallel)'s chunks: about k maps in key order, sharing the nodes.</summary>
    public static OrderedMap<TK, TV>[] Split<TK, TV>(OrderedMap<TK, TV> map, int k) => map.Split(k);

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
    // Many maps at once
    //
    // Two ways, chosen by size (measured on int keys, 1M keys in all):
    //  - A transient on one input, with the others set into it. Each key set
    //    costs a descent, so it pays when the others hold few keys: a map of
    //    1M and 10 of 10k took 15 ms this way, 41 ms the other.
    //  - A k-way merge of the inputs in key order into a builder, which
    //    builds the tree bottom-up with no sort. It pays when the keys are
    //    spread over the inputs: 10 maps of 100k took 48 ms this way, 83 ms
    //    the other, and 111 ms by Merge two at a time. Its heap costs log k
    //    a key, so with many inputs the transient catches up: at 100 inputs
    //    they were level.
    // Inputs ordered by different comparers are merged two at a time.
    // ---------------------------------------------------------

    /// <summary>
    ///     The k-way merge is used when the inputs other than the biggest hold at least this
    ///     share of the keys, in percent.
    /// </summary>
    public const int ManyMergePercent = 30;

    /// <summary>The k-way merge is used for at most this many inputs.</summary>
    public const int ManyMergeMaxInputs = 32;

    /// <summary>
    ///     <paramref name="map" />, <paramref name="other" /> and each map in
    ///     <paramref name="more" />, each laid over the ones before it: where several have a
    ///     key, the last one's value wins.
    /// </summary>
    public static OrderedMap<TK, TV> MergeAll<TK, TV>(
        OrderedMap<TK, TV> map,
        OrderedMap<TK, TV> other,
        OrderedMap<TK, TV>[] more)
    {
        var inputs = NonEmpty(map, other, more);
        if (inputs.Count == 0) return map;
        if (inputs.Count == 1) return inputs[0];
        if (!SameOrder(inputs)) return Fold(inputs, Merge);

        var (biggest, total) = Biggest(inputs);
        if (!KWayPays(inputs.Count, inputs[biggest].Count, total))
        {
            // The ones after the biggest are set over it. The ones before it only add keys that
            // are not there yet, latest first, so that no later value is replaced.
            var transient = inputs[biggest].ToTransient();
            for (var i = biggest + 1; i < inputs.Count; i++)
                foreach (var kvp in inputs[i]) transient.Set(kvp.Key, kvp.Value);
            for (var i = biggest - 1; i >= 0; i--)
                foreach (var kvp in inputs[i])
                    if (!transient.ContainsKey(kvp.Key)) transient.Set(kvp.Key, kvp.Value);
            return transient.ToPersistent();
        }

        return KWay(inputs, total, null);
    }

    /// <summary>
    ///     The same, with the collisions decided by <paramref name="resolver" />: the values of
    ///     a key are combined from the first map to the last, as merging two at a time would.
    /// </summary>
    public static OrderedMap<TK, TV> MergeWithAll<TK, TV>(
        Func<TK, TV, TV, TV> resolver,
        OrderedMap<TK, TV> map,
        OrderedMap<TK, TV> other,
        OrderedMap<TK, TV>[] more)
    {
        var inputs = NonEmpty(map, other, more);
        if (inputs.Count == 0) return map;
        if (inputs.Count == 1) return inputs[0];
        if (!SameOrder(inputs)) return Fold(inputs, [NonUserCode] (a, b) => MergeWith(resolver, a, b));

        var (biggest, total) = Biggest(inputs);
        if (KWayPays(inputs.Count, inputs[biggest].Count, total)) return KWay(inputs, total, resolver);

        // A transient on the biggest. The values of a key are combined in input order: the
        // maps before the biggest are merged first, and their values come before its own.
        var transient = inputs[biggest].ToTransient();
        if (biggest > 0)
        {
            var before = inputs[0];
            if (biggest > 1)
                before = MergeWithAll(resolver, inputs[0], inputs[1], inputs.GetRange(2, biggest - 2).ToArray());
            foreach (var kvp in before)
                transient.Set(
                    kvp.Key,
                    transient.TryGetValue(kvp.Key, out var own) ? resolver(kvp.Key, kvp.Value, own) : kvp.Value);
        }
        for (var i = biggest + 1; i < inputs.Count; i++)
            foreach (var kvp in inputs[i])
                transient.Set(
                    kvp.Key,
                    transient.TryGetValue(kvp.Key, out var so) ? resolver(kvp.Key, so, kvp.Value) : kvp.Value);
        return transient.ToPersistent();
    }

    // The inputs that have keys, in order.
    private static List<OrderedMap<TK, TV>> NonEmpty<TK, TV>(
        OrderedMap<TK, TV> map, OrderedMap<TK, TV> other, OrderedMap<TK, TV>[] more)
    {
        var inputs = new List<OrderedMap<TK, TV>>(more.Length + 2);
        if (map.Count > 0) inputs.Add(map);
        if (other.Count > 0) inputs.Add(other);
        foreach (var m in more)
            if (m.Count > 0) inputs.Add(m);
        return inputs;
    }

    private static bool SameOrder<TK, TV>(List<OrderedMap<TK, TV>> inputs)
    {
        var first = inputs[0].Comparer;
        foreach (var m in inputs)
            if (!ReferenceEquals(m.Comparer, first) && !m.Comparer.Equals(first)) return false;
        return true;
    }

    private static OrderedMap<TK, TV> Fold<TK, TV>(
        List<OrderedMap<TK, TV>> inputs, Func<OrderedMap<TK, TV>, OrderedMap<TK, TV>, OrderedMap<TK, TV>> merge)
    {
        var result = inputs[0];
        for (var i = 1; i < inputs.Count; i++) result = merge(result, inputs[i]);
        return result;
    }

    private static (int Index, long Total) Biggest<TK, TV>(List<OrderedMap<TK, TV>> inputs)
    {
        var biggest = 0;
        long total = 0;
        for (var i = 0; i < inputs.Count; i++)
        {
            total += inputs[i].Count;
            if (inputs[i].Count > inputs[biggest].Count) biggest = i;
        }
        return (biggest, total);
    }

    private static bool KWayPays(int inputs, long biggest, long total) =>
        inputs <= ManyMergeMaxInputs && (total - biggest) * 100 >= total * ManyMergePercent;

    // The inputs walked in key order at once. A heap holds the input of each next key, by
    // the key alone: the inputs that have one key are then put in input order, so that the
    // last one's value wins or the values are combined first to last. A key is in few inputs,
    // so that sort is short.
    private static OrderedMap<TK, TV> KWay<TK, TV>(
        List<OrderedMap<TK, TV>> inputs, long total, Func<TK, TV, TV, TV>? resolver)
    {
        var comparer = inputs[0].Comparer;
        var walks = new BTreeEnumerator<TK, TV>[inputs.Count];
        var heap = new PriorityQueue<int, TK>(inputs.Count, comparer);
        for (var i = 0; i < walks.Length; i++)
        {
            walks[i] = inputs[i].GetEnumerator();
            if (walks[i].MoveNext()) heap.Enqueue(i, walks[i].Current.Key);
        }

        var builder = new OrderedMapBuilder<TK, TV>((int)Math.Min(total, int.MaxValue), comparer);
        var same = new List<int>();
        while (heap.TryDequeue(out var input, out var key))
        {
            if (!heap.TryPeek(out _, out var nextKey) || comparer.Compare(nextKey, key) != 0)
            {
                builder.Add(key, walks[input].Current.Value);
                if (walks[input].MoveNext()) heap.Enqueue(input, walks[input].Current.Key);
                continue;
            }

            same.Clear();
            same.Add(input);
            while (heap.TryPeek(out var next, out nextKey) && comparer.Compare(nextKey, key) == 0)
            {
                heap.Dequeue();
                same.Add(next);
            }
            same.Sort();

            var value = walks[same[0]].Current.Value;
            for (var j = 1; j < same.Count; j++)
            {
                var later = walks[same[j]].Current.Value;
                value = resolver == null ? later : resolver(key, value, later);
            }
            builder.Add(key, value);

            foreach (var i in same)
                if (walks[i].MoveNext()) heap.Enqueue(i, walks[i].Current.Key);
        }

        return builder.Build();
    }

    // ---------------------------------------------------------
    // Navigation — what the ordering buys over a hashed map
    // ---------------------------------------------------------

    public static bool TryGetMin<TK, TV>(OrderedMap<TK, TV> map, [MaybeNullWhen(false)] out TK key, [MaybeNullWhen(false)] out TV value) =>
        map.TryGetMin(out key, out value);

    public static bool TryGetMax<TK, TV>(OrderedMap<TK, TV> map, [MaybeNullWhen(false)] out TK key, [MaybeNullWhen(false)] out TV value) =>
        map.TryGetMax(out key, out value);

    /// <summary>
    ///     The entry whose key is the smallest one greater than <paramref name="key" />, which
    ///     need not itself be in the map.
    /// </summary>
    public static bool TryGetSuccessor<TK, TV>(OrderedMap<TK, TV> map, TK key, [MaybeNullWhen(false)] out TK nextKey, [MaybeNullWhen(false)] out TV nextValue) =>
        map.TryGetSuccessor(key, out nextKey, out nextValue);

    public static bool TryGetPredecessor<TK, TV>(OrderedMap<TK, TV> map, TK key, [MaybeNullWhen(false)] out TK prevKey, [MaybeNullWhen(false)] out TV prevValue) =>
        map.TryGetPredecessor(key, out prevKey, out prevValue);

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
        return !Iter<TK, TV>([NonUserCode] (key, value) => !predicate(key, value), map);
    }

    /// <summary>The first entry in key order satisfying <paramref name="predicate" />, if any.</summary>
    public static bool TryFind<TK, TV>(
        Func<TK, TV, bool> predicate,
        OrderedMap<TK, TV> map,
        [MaybeNullWhen(false)] out TK key,
        [MaybeNullWhen(false)] out TV value)
    {
        foreach (var kvp in map)
        {
            if (predicate(kvp.Key, kvp.Value))
            {
                key = kvp.Key;
                value = kvp.Value;
                return true;
            }
        }

        key = default;
        value = default;
        return false;
    }

    // ---------------------------------------------------------
    // Walking
    // ---------------------------------------------------------

    /// <summary>The cursor on the first entry of the map, or a done cursor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderedMapCursor<TK, TV> Cursor<TK, TV>(OrderedMap<TK, TV> map) =>
        OrderedMapCursor<TK, TV>.Start(map.Root);

    /// <summary>True when the cursor is past the last entry. Does not move the cursor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CursorDone<TK, TV>(OrderedMapCursor<TK, TV> cursor) => cursor.Done;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TK, TV) CursorCurrent<TK, TV>(OrderedMapCursor<TK, TV> cursor) => cursor.Current;

    /// <summary>The cursor on the next entry. The cursor given does not change.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OrderedMapCursor<TK, TV> CursorNext<TK, TV>(OrderedMapCursor<TK, TV> cursor) => cursor.Next();
}
