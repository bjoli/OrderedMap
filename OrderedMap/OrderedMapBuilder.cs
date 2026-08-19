using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OrderedMap;

public sealed class OrderedMapBuilder<TK, TV>
{
    private readonly List<KeyValuePair<TK, TV>> _items;
    private readonly IComparer<TK> _comparer;

    public OrderedMapBuilder(IComparer<TK>? comparer = null)
    {
        _items = new List<KeyValuePair<TK, TV>>();
        _comparer = comparer ?? Comparer<TK>.Default;
    }

    public OrderedMapBuilder(int capacity, IComparer<TK>? comparer = null)
    {
        _items = new List<KeyValuePair<TK, TV>>(capacity);
        _comparer = comparer ?? Comparer<TK>.Default;
    }

    public void Add(TK key, TV value)
    {
        _items.Add(new KeyValuePair<TK, TV>(key, value));
    }

    public OrderedMap<TK, TV> Build()
    {
        if (_items.Count == 0)
        {
            return OrderedMap<TK, TV>.Empty(_comparer);
        }

        // We need a stable sort. Since List<T>.Sort() and Array.Sort() are unstable,
        // we attach the original index to preserve insertion order for duplicate keys.
        var array = new (KeyValuePair<TK, TV> item, int index)[_items.Count];
        for (int i = 0; i < _items.Count; i++)
        {
            array[i] = (_items[i], i);
        }

        Array.Sort(array, (a, b) =>
        {
            int cmp = _comparer.Compare(a.item.Key, b.item.Key);
            if (cmp == 0)
            {
                cmp = a.index.CompareTo(b.index);
            }
            return cmp;
        });

        // Compact duplicates (last one wins, based on original insertion order / stable sort)
        int uniqueCount = 0;
        for (int i = 0; i < array.Length; i++)
        {
            if (uniqueCount > 0 && _comparer.Compare(array[i].item.Key, array[uniqueCount - 1].item.Key) == 0)
            {
                // Overwrite the previous one
                array[uniqueCount - 1] = array[i];
            }
            else
            {
                array[uniqueCount++] = array[i];
            }
        }

        var uniqueSpan = new ReadOnlySpan<(KeyValuePair<TK, TV> item, int index)>(array, 0, uniqueCount);
        var root = BuildNodes(uniqueSpan);
        return new OrderedMap<TK, TV>(root, _comparer, uniqueCount);
    }

    private Node<TK> BuildNodes(ReadOnlySpan<(KeyValuePair<TK, TV> item, int index)> span)
    {
        // 1. Build leaf nodes
        int numLeaves = (span.Length + LeafNode<TK, TV>.Capacity - 1) / LeafNode<TK, TV>.Capacity;
        var leaves = new Node<TK>[numLeaves];

        for (int i = 0; i < numLeaves; i++)
        {
            int offset = i * LeafNode<TK, TV>.Capacity;
            int count = Math.Min(LeafNode<TK, TV>.Capacity, span.Length - offset);

            var leaf = new LeafNode<TK, TV>(OwnerId.None);
            for (int j = 0; j < count; j++)
            {
                leaf.Keys![j] = span[offset + j].item.Key;
                leaf.Values[j] = span[offset + j].item.Value;
            }
            leaf.SetCount(count);
            leaves[i] = leaf;
        }

        // 2. Build internal nodes bottom-up until we have a single root
        Node<TK>[] currentLevel = leaves;

        while (currentLevel.Length > 1)
        {
            int numParents = (currentLevel.Length + InternalNode<TK>.Capacity - 1) / InternalNode<TK>.Capacity;
            var parents = new Node<TK>[numParents];

            for (int i = 0; i < numParents; i++)
            {
                int offset = i * InternalNode<TK>.Capacity;
                int count = Math.Min(InternalNode<TK>.Capacity, currentLevel.Length - offset);

                var internalNode = new InternalNode<TK>(OwnerId.None);
                var childrenSpan = MemoryMarshal.CreateSpan(ref internalNode.Children[0]!, count);
                var keysSpan = MemoryMarshal.CreateSpan(ref internalNode.Keys[0], count - 1);

                for (int j = 0; j < count; j++)
                {
                    childrenSpan[j] = currentLevel[offset + j];
                    
                    if (j < count - 1)
                    {
                        // The routing key is the first key of the right child
                        keysSpan[j] = GetFirstKey(currentLevel[offset + j + 1]);
                    }
                }
                
                // An internal node has N children, so Count is N-1
                internalNode.SetCount(count - 1);
                parents[i] = internalNode;
            }

            currentLevel = parents;
        }

        return currentLevel[0];
    }

    private TK GetFirstKey(Node<TK> node)
    {
        while (!node.IsLeaf)
        {
            var internalNode = node.AsInternal();
            node = internalNode.GetChildren()[0];
        }
        return node.AsLeaf<TV>().Keys![0];
    }
}
