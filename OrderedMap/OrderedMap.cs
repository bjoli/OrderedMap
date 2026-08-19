using System.Collections.Generic;

namespace OrderedMap;

public sealed class OrderedMap<TK, TV> : BaseOrderedMap<TK, TV>
{
    internal OrderedMap(Node<TK> root, IComparer<TK> comparer, int count) 
        : base(root, comparer, count) { }

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
    
        return new OrderedMap<TK, TV>(emptyRoot, comparer ?? Comparer<TK>.Default, 0);
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
