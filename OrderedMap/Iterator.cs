using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OrderedMap;



public struct BTreeEnumerable<TK, TV> : IEnumerable<KeyValuePair<TK, TV>>
{
    private readonly Node<TK> _root;
    private readonly IComparer<TK> _comparer;
    private readonly TK _min, _max;
    private readonly bool _hasMin, _hasMax;

    public BTreeEnumerable(Node<TK> root, IComparer<TK> comparer, bool hasMin, TK min, bool hasMax, TK max)
    {
        _root = root; _comparer = comparer;
        _hasMin = hasMin; _min = min;
        _hasMax = hasMax; _max = max;
    }

    public BTreeEnumerator<TK, TV> GetEnumerator()
    {
        return new BTreeEnumerator<TK, TV>(_root, _comparer, _hasMin, _min, _hasMax, _max);
    }

    IEnumerator<KeyValuePair<TK, TV>> IEnumerable<KeyValuePair<TK, TV>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Fixed-size buffer for the path. 
// Depth 16 * 32 (branching factor) = Exabytes of capacity.
// The B tree is, theoretically, not limited by the size of an int, like
// all int-indexed data structures (or bit partitioned ones).
// This should be enough for anyone.
[InlineArray(16)]
internal struct IterNodeBuffer<TK>
{
    private Node<TK> _element0;
}

[InlineArray(16)]
internal struct IterIndexBuffer
{
    private int _element0;
}

public struct BTreeEnumerator<TK, TV> : IEnumerator<KeyValuePair<TK, TV>>
    {
        private readonly IComparer<TK> _comparer;
        private readonly Node<TK> _root;
        
        // --- BOUNDS ---
        private readonly bool _hasMax;
        private readonly TK _maxKey;
        private readonly bool _hasMin;
        private readonly TK _minKey;

        // --- INLINE STACK ---
        private IterNodeBuffer<TK> _nodeStack; 
        private IterIndexBuffer _indexStack;
        private int _depth;

        // --- STATE ---
        private LeafNode<TK, TV>? _currentLeaf;
        private int _currentLeafIndex;
        private KeyValuePair<TK, TV> _current;

        // Unified Constructor
        // We use boolean flags because 'K' might be a struct where 'null' is impossible.
        public BTreeEnumerator(Node<TK>? root, IComparer<TK> comparer, bool hasMin, TK minKey, bool hasMax, TK maxKey)
        {
            _root = root!;
            _comparer = comparer;
            _hasMax = hasMax;
            _maxKey = maxKey;
            _hasMin = hasMin;
            _minKey = minKey;
            
            _nodeStack = new IterNodeBuffer<TK>();
            _indexStack = new IterIndexBuffer(); // Explicit struct init
            _depth = 0;
            _currentLeaf = null;
            _currentLeafIndex = -1;
            _current = default;

            if (root != null)
            {
                if (hasMin)
                {
                    Seek(minKey);
                }
                else
                {
                    DiveLeft();
                }
            }
        }

        // Logic 1: Unbounded Start (Go to very first item)
        private void DiveLeft()
        {
            Node<TK> node = _root;
            _depth = 0;

            while (!node.IsLeaf)
            {
                var internalNode = node.AsInternal();
                _nodeStack[_depth] = internalNode;
                _indexStack[_depth] = 0; // Always take left-most child
                _depth++;
                node = internalNode.Children[0]!;
            }

            _currentLeaf = node.AsLeaf<TV>();
            _currentLeafIndex = -1; // Position before the first element (0)
        }

        // Logic 2: Bounded Start (Go to specific key)
        private void Seek(TK key)
        {
            Node<TK> node = _root;
            _depth = 0;

            // Dive using Routing
            while (!node.IsLeaf)
            {
                var internalNode = node.AsInternal();
                int idx = BTreeFunctions.FindRoutingIndex(internalNode, key, _comparer);
                
                _nodeStack[_depth] = internalNode;
                _indexStack[_depth] = idx;
                _depth++;

                node = internalNode.Children[idx]!;
            }

            // Find index in Leaf
            _currentLeaf = node.AsLeaf<TV>();
            int index = BTreeFunctions.FindIndex(_currentLeaf, key, _comparer);
            
            // Set position to (index - 1) so that the first MoveNext() lands on 'index'
            _currentLeafIndex = index - 1;
        }

        public bool MoveNext()
        {
            if (_currentLeaf == null) return false;

            // 1. Try to advance in current leaf
            if (++_currentLeafIndex < _currentLeaf.Header.Count)
            {
                // OPTIMIZATION: Check Max Bound (if active)
                if (_hasMax)
                {
                     // If Current Key > Max Key, we are done.
                     if (_comparer.Compare(_currentLeaf.Keys![_currentLeafIndex], _maxKey) > 0)
                     {
                         _currentLeaf = null; // Close iterator
                         return false;
                     }
                }

                _current = new KeyValuePair<TK, TV>(_currentLeaf.Keys![_currentLeafIndex], _currentLeaf.Values[_currentLeafIndex]);
                return true;
            }

            // 2. Leaf exhausted. Find next leaf.
            if (FindNextLeaf())
            {
                // Found new leaf, index reset to 0. 
                // Check Max Bound immediately for the first item
                if (_hasMax)
                {
                    if (_comparer.Compare(_currentLeaf.Keys![0], _maxKey) > 0)
                    {
                        _currentLeaf = null;
                        return false;
                    }
                }

                _current = new KeyValuePair<TK, TV>(_currentLeaf.Keys![0], _currentLeaf.Values[0]);
                return true;
            }

            return false;
        }

        private bool FindNextLeaf()
        {
            while (_depth > 0)
            {
                _depth--;
                var internalNode = _nodeStack[_depth].AsInternal();
                int currentIndex = _indexStack[_depth];

                if (currentIndex < internalNode.Header.Count)
                {
                    int nextIndex = currentIndex + 1;
                    _indexStack[_depth] = nextIndex;
                    _depth++;

                    Node<TK> node = internalNode.Children[nextIndex]!;
                    while (!node.IsLeaf)
                    {
                        _nodeStack[_depth] = node;
                        _indexStack[_depth] = 0;
                        _depth++;
                        node = node.AsInternal().Children[0]!;
                    }

                    _currentLeaf = node.AsLeaf<TV>();
                    _currentLeafIndex = 0;
                    return true;
                }
            }
            return false;
        }

        public KeyValuePair<TK, TV> Current => _current;
        object IEnumerator.Current => _current;
        public void Reset()
        {
            // 1. Clear current state
            _depth = 0;
            _currentLeaf = null;
            _currentLeafIndex = -1;
            _current = default;

            // 2. Re-initialize based on how the iterator was created
            if (_root != null)
            {
                if (_hasMin)
                {
                    // If we had a start range, find it again
                    Seek(_minKey);
                }
                else
                {
                    // Otherwise, go back to the very first leaf
                    DiveLeft();
                }
            }
        }
        public void Dispose() { }
    }
