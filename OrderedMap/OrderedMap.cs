using System;
using System.Collections.Generic;

namespace OrderedMap;

public sealed class OrderedMap<TK, TV> : BaseOrderedMap<TK, TV>, IEquatable<OrderedMap<TK, TV>>
{
    internal OrderedMap(Node<TK> root, IComparer<TK> comparer, int count) 
        : base(root, comparer, count) { }

    // ---------------------------------------------------------
    // Structural equality
    // ---------------------------------------------------------
    //
    // Here for the same reason `Map<TK, TV>` has it, and — more to the point —
    // because `=` on two of these is `std/orderedmap`'s implementation while
    // every hash-based collection and every .NET consumer asks this instead.
    // The two answering differently is the one thing equality must never do.
    //
    // `EqualityComparer` and not `Comparer`: the tree's ordering says which slot
    // a key goes in, not whether two keys are the same thing. For a type the
    // Bjolang compiler emitted, `EqualityComparer` *is* that type's own `Eq`
    // implementation.
    //
    // A paired walk, which is what the ordering buys: both trees enumerate in
    // key order, so keys and values alike are compared in place with no lookup.
    public bool Equals(OrderedMap<TK, TV>? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || Count != other.Count) return false;

        var keys = EqualityComparer<TK>.Default;
        var values = EqualityComparer<TV>.Default;
        using var mine = GetEnumerator();
        using var theirs = other.GetEnumerator();

        while (mine.MoveNext() && theirs.MoveNext())
        {
            if (!keys.Equals(mine.Current.Key, theirs.Current.Key)) return false;
            if (!values.Equals(mine.Current.Value, theirs.Current.Value)) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is OrderedMap<TK, TV> other && Equals(other);

    // The same fold `eq-hash` performs in `std/orderedmap`, seed and all, so
    // that the two cannot answer different numbers for one map.
    public override int GetHashCode()
    {
        var keys = EqualityComparer<TK>.Default;
        var values = EqualityComparer<TV>.Default;
        var hash = 17;

        foreach (var entry in this)
        {
            var k = entry.Key is null ? 0 : keys.GetHashCode(entry.Key);
            var v = entry.Value is null ? 0 : values.GetHashCode(entry.Value);
            hash = HashCode.Combine(hash, HashCode.Combine(k, v));
        }

        return hash;
    }

    // ---------------------------------------------------------
    // Immutable Write API (Returns new Map)
    // ---------------------------------------------------------
    public OrderedMap<TK, TV> Set(TK key, TV value)
    {
        // OPTIMIZATION: Use OwnerId.None (0).
        // This signals EnsureEditable to always copy the root path, 
        // producing a new tree of nodes that also have OwnerId.None.
        var newRoot = BTreeFunctions.Set(Root, key, value, Comparer, OwnerId.None, out bool countChanged);
        return new OrderedMap<TK, TV>(newRoot, Comparer, countChanged ? Count + 1 : Count);
    }
   
    public static OrderedMap<TK, TV> Empty(IComparer<TK>? comparer = null)
    {
        // Create an empty Leaf Node.
        // 'default(OwnerId)' (usually 0) marks this node as Immutable/Persistent.
        // This ensures that any subsequent Set/Remove will clone this node 
        // instead of modifying it in place.
        var emptyRoot = new LeafNode<TK, TV>(default(OwnerId));
    
        return new OrderedMap<TK, TV>(emptyRoot, comparer ?? DefaultOrder.For<TK>(), 0);
    }

    public OrderedMap<TK, TV> Remove(TK key)
    {
        var newRoot = BTreeFunctions.Remove<TK,TV>(Root, key, Comparer, OwnerId.None, out bool removed);
        if (!removed) return this;
        return new OrderedMap<TK, TV>(newRoot, Comparer, Count - 1);
    }

    public TransientOrderedMap<TK, TV> ToTransient()
    {
        return new TransientOrderedMap<TK, TV>(Root, Comparer, Count);
    }
}
