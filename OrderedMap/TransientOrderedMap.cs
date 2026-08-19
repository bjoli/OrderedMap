using System.Collections.Generic;

namespace OrderedMap;

public sealed class TransientOrderedMap<TK, TV> : BaseOrderedMap<TK, TV>
{
    // This is mutable, but we treat it as readonly for the ID generation logic usually.
    private OwnerId _transactionId;

    public TransientOrderedMap(Node<TK> root, IComparer<TK> comparer, int count) 
        : base(root, comparer, count) 
    {
        _transactionId = OwnerId.Next();
    }

    public void Set(TK key, TV value)
    {
        Root = BTreeFunctions.Set(Root, key, value, Comparer, _transactionId, out bool countChanged);
        if (countChanged) Count++;
    }

    public void Remove(TK key)
    {
        Root = BTreeFunctions.Remove<TK,TV>(Root, key, Comparer, _transactionId, out bool removed);
        if (removed) Count--;
    }

    public OrderedMap<TK, TV> ToPersistent()
    {
        // 1. Create the snapshot by copying all relevant information
            
        var snapshot = new OrderedMap<TK, TV>(Root, Comparer, Count);

        // 2. Protect the snapshot from THIS TransientOrderedMap by getting a new ownerId 
        // so that future edits will be done by CoW
        _transactionId = OwnerId.Next();
            
        return snapshot;
    }
}