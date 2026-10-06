using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OrderedMap;

public sealed class OrderedMapBuilder<TK, TV>
{
    private readonly List<KeyValuePair<TK, TV>> _items;
    private readonly IComparer<TK> _comparer;

    // Every key so far was greater than the one before it. Walking a map, or mapping
    // over one, appends in key order, and Build then needs neither a sort nor a
    // pass for duplicates.
    private bool _ascending = true;

    public OrderedMapBuilder(IComparer<TK>? comparer = null)
    {
        _items = new List<KeyValuePair<TK, TV>>();
        _comparer = comparer ?? DefaultOrder.For<TK>();
    }

    public OrderedMapBuilder(int capacity, IComparer<TK>? comparer = null)
    {
        _items = new List<KeyValuePair<TK, TV>>(capacity);
        _comparer = comparer ?? DefaultOrder.For<TK>();
    }

    public void Add(TK key, TV value)
    {
        if (_ascending && _items.Count > 0 && _comparer.Compare(_items[^1].Key, key) >= 0)
        {
            _ascending = false;
        }
        _items.Add(new KeyValuePair<TK, TV>(key, value));
    }

    public void AddRange(IEnumerable<KeyValuePair<TK, TV>> range)
    {
        foreach (var kvp in range) Add(kvp.Key, kvp.Value);
    }

    /// <summary>
    ///     How many pairs have been appended. A key appended twice is counted twice: duplicates
    ///     are resolved by <see cref="Build" />, last one winning, so this is the number of
    ///     appends rather than the size of the map being built.
    /// </summary>
    public int Count => _items.Count;

    public OrderedMap<TK, TV> Build()
    {
        if (_items.Count == 0)
        {
            return OrderedMap<TK, TV>.Empty(_comparer);
        }

        if (_ascending)
        {
            ReadOnlySpan<KeyValuePair<TK, TV>> items = CollectionsMarshal.AsSpan(_items);
            return new OrderedMap<TK, TV>(BuildNodes(items), _comparer, items.Length);
        }

        // Array.Sort is unstable, so the original index breaks ties: among equal keys the
        // last one appended sorts last, and wins below.
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

        var unique = new KeyValuePair<TK, TV>[array.Length];
        int uniqueCount = 0;
        for (int i = 0; i < array.Length; i++)
        {
            if (uniqueCount > 0 && _comparer.Compare(array[i].item.Key, unique[uniqueCount - 1].Key) == 0)
            {
                unique[uniqueCount - 1] = array[i].item;
            }
            else
            {
                unique[uniqueCount++] = array[i].item;
            }
        }

        var uniqueSpan = new ReadOnlySpan<KeyValuePair<TK, TV>>(unique, 0, uniqueCount);
        return new OrderedMap<TK, TV>(BuildNodes(uniqueSpan), _comparer, uniqueCount);
    }

    /// <summary>The tree over <paramref name="span" />, which is in strictly ascending key order.</summary>
    private Node<TK> BuildNodes(ReadOnlySpan<KeyValuePair<TK, TV>> span)
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
                leaf.Keys![j] = span[offset + j].Key;
                leaf.Values[j] = span[offset + j].Value;
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
