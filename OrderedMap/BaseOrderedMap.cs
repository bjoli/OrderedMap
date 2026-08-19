using System;
using System.Collections;
using System.Collections.Generic;

namespace OrderedMap;

public abstract class BaseOrderedMap<TK, TV> : IEnumerable<KeyValuePair<TK, TV>>
{
    internal Node<TK> Root;
    internal readonly IComparer<TK> Comparer;
    
    public int Count { get; protected set; }

    protected BaseOrderedMap(Node<TK> root, IComparer<TK> comparer, int count)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Comparer = comparer ?? Comparer<TK>.Default;
        Count = count;
    }

    // ---------------------------------------------------------
    // Read Operations (Shared)
    // ---------------------------------------------------------

    public bool TryGetValue(TK key, out TV value)
    {
        return BTreeFunctions.TryGetValue(Root, key, Comparer, out value);
    }

    public bool ContainsKey(TK key)
    {
        return BTreeFunctions.TryGetValue<TK,TV>(Root, key, Comparer, out _);
    }
    
    // ---------------------------------------------------------
    // Bootstrap / Factory Helpers
    // ---------------------------------------------------------
        
    public static OrderedMap<TK, TV> Create(IComparer<TK>? comparer = null)
    {
        // Start with an empty leaf owned by None so the first write triggers CoW.
        var emptyRoot = new LeafNode<TK, TV>(OwnerId.None);
        return new OrderedMap<TK, TV>(emptyRoot, comparer ?? Comparer<TK>.Default, 0);
    }

    public static TransientOrderedMap<TK, TV> CreateTransient(IComparer<TK>? comparer = null)
    {
        var emptyRoot = new LeafNode<TK, TV>(OwnerId.None);
        return new TransientOrderedMap<TK, TV>(emptyRoot, comparer ?? Comparer<TK>.Default, 0);
    }
    
    
    public BTreeEnumerator<TK, TV> GetEnumerator()
    {
        return AsEnumerable().GetEnumerator();
    }

    IEnumerator<KeyValuePair<TK, TV>> IEnumerable<KeyValuePair<TK, TV>>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    // 1. Full Scan
    public BTreeEnumerable<TK, TV> AsEnumerable() 
        => new(Root, Comparer, false, default!, false, default!);

    // 2. Exact Range
    public BTreeEnumerable<TK, TV> Range(TK min, TK max) 
        => new(Root, Comparer, true, min, true, max);

    // 3. Start From (Open Ended)
    public BTreeEnumerable<TK, TV> From(TK min) => new(Root, Comparer, true, min, false, default!);

    // 4. Until (Start at beginning)
    public BTreeEnumerable<TK, TV> Until(TK max) 
        => new(Root, Comparer, false, default!, true, max);

    // ---------------------------------------------------------
    // Navigation Operations
    // ---------------------------------------------------------

    public bool TryGetMin(out TK key, out TV value) => BTreeFunctions.TryGetMin(Root, out key, out value);

    public bool TryGetMax(out TK key, out TV value) => BTreeFunctions.TryGetMax(Root, out key, out value);

    public bool TryGetSuccessor(TK key, out TK nextKey, out TV nextValue) => BTreeFunctions.TryGetSuccessor(Root, key, Comparer, out nextKey, out nextValue);

    public bool TryGetPredecessor(TK key, out TK prevKey, out TV prevValue) => BTreeFunctions.TryGetPredecessor(Root, key, Comparer, out prevKey, out prevValue);

    // ---------------------------------------------------------
    // Set Operations (Linear Merge O(N+M))
    // ---------------------------------------------------------

    public IEnumerable<KeyValuePair<TK, TV>> Intersect(BaseOrderedMap<TK, TV> other)
    {
        using var enum1 = this.GetEnumerator();
        using var enum2 = other.GetEnumerator();
        
        bool has1 = enum1.MoveNext();
        bool has2 = enum2.MoveNext();

        while (has1 && has2)
        {
            int cmp = Comparer.Compare(enum1.Current.Key, enum2.Current.Key);
            if (cmp == 0)
            {
                yield return enum1.Current;
                has1 = enum1.MoveNext();
                has2 = enum2.MoveNext();
            }
            else if (cmp < 0) has1 = enum1.MoveNext();
            else has2 = enum2.MoveNext();
        }
    }

    public IEnumerable<KeyValuePair<TK, TV>> Except(BaseOrderedMap<TK, TV> other)
    {
        using var enum1 = this.GetEnumerator();
        using var enum2 = other.GetEnumerator();
        
        bool has1 = enum1.MoveNext();
        bool has2 = enum2.MoveNext();

        while (has1 && has2)
        {
            int cmp = Comparer.Compare(enum1.Current.Key, enum2.Current.Key);
            if (cmp == 0)
            {
                has1 = enum1.MoveNext();
                has2 = enum2.MoveNext();
            }
            else if (cmp < 0)
            {
                yield return enum1.Current;
                has1 = enum1.MoveNext();
            }
            else
            {
                has2 = enum2.MoveNext();
            }
        }

        while (has1)
        {
            yield return enum1.Current;
            has1 = enum1.MoveNext();
        }
    }

    public IEnumerable<KeyValuePair<TK, TV>> SymmetricExcept(BaseOrderedMap<TK, TV> other)
    {
        using var enum1 = this.GetEnumerator();
        using var enum2 = other.GetEnumerator();
        
        bool has1 = enum1.MoveNext();
        bool has2 = enum2.MoveNext();

        while (has1 && has2)
        {
            int cmp = Comparer.Compare(enum1.Current.Key, enum2.Current.Key);
            if (cmp == 0)
            {
                has1 = enum1.MoveNext();
                has2 = enum2.MoveNext();
            }
            else if (cmp < 0)
            {
                yield return enum1.Current;
                has1 = enum1.MoveNext();
            }
            else
            {
                yield return enum2.Current;
                has2 = enum2.MoveNext();
            }
        }

        while (has1)
        {
            yield return enum1.Current;
            has1 = enum1.MoveNext();
        }
        while (has2)
        {
            yield return enum2.Current;
            has2 = enum2.MoveNext();
        }
    }
}
