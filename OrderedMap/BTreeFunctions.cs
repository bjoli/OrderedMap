using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OrderedMap
{
    public static class BTreeFunctions
    {
        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        public static bool TryGetValue<TK, TV>(Node<TK> root, TK key, IComparer<TK> comparer, out TV value)
        {
            Node<TK> current = root;
            while (true)
            {
                if (current.IsLeaf)
                {
                    var leaf = current.AsLeaf<TV>();
                    int index = FindIndex(leaf, key, comparer);
                    if (index < leaf.Header.Count && comparer.Compare(leaf.Keys![index], key) == 0)
                    {
                        value = leaf.Values[index];
                        return true;
                    }
                    value = default!;
                    return false;
                }
                else
                {
                    var internalNode = current.AsInternal();
                    int index = FindRoutingIndex(internalNode, key, comparer);
                    current = internalNode.Children[index]!;
                }
            }
        }

        public static Node<TK> Set<TK, TV>(Node<TK> root, TK key, TV value, IComparer<TK> comparer, OwnerId owner, out bool countChanged)
        {
            root = root.EnsureEditable(owner);

            var (newNode, sep) = InsertRecursive(root, key, value, comparer, owner, out countChanged);

            if (newNode != null)
            {
                var newRoot = new InternalNode<TK>(owner);
                
                newRoot.Keys[0] = sep;
                newRoot.Children[0] = root;
                newRoot.Children[1] = newNode;
                newRoot.SetCount(1);
                
                return newRoot;
            }

            return root;
        }

        private static (Node<TK>? newNode, TK separator ) InsertRecursive<TK, TV>(Node<TK> node, TK key, TV value, IComparer<TK> comparer, OwnerId owner, out bool added)
        {
            if (node.IsLeaf)
            {
                var leaf = node.AsLeaf<TV>();
                int index = FindIndex(leaf, key, comparer);

                if (index < leaf.Header.Count && comparer.Compare(leaf.Keys![index], key) == 0)
                {
                    leaf.Values[index] = value;
                    added = false;
                    return (null, default)!;
                }

                added = true;
                if (leaf.Header.Count < LeafNode<TK, TV>.Capacity)
                {
                    InsertIntoLeaf(leaf, index, key, value);
                    return (null, default)!;
                }
                else
                {
                    return SplitLeaf(leaf, index, key, value, owner);
                }
            }
            else
            {
                var internalNode = node.AsInternal();
                int index = FindRoutingIndex(internalNode, key, comparer);

                var child = internalNode.Children[index]!.EnsureEditable(owner);
                internalNode.Children[index] = child;

                var (newNode, sep) = InsertRecursive(child, key, value, comparer, owner, out added);

                if (newNode != null)
                {
                    if (internalNode.Header.Count < InternalNode<TK>.Capacity - 1)
                    {
                        InsertIntoInternal(internalNode, index, sep, newNode);
                        return (null, default)!;
                    }
                    
                    return SplitInternal(internalNode, index, sep, newNode, owner);
                }
                return (null, default)!;
            }
        }

        public static Node<TK> Remove<TK, TV>(Node<TK> root, TK key, IComparer<TK> comparer, OwnerId owner, out bool countChanged)
        {
            root = root.EnsureEditable(owner);

            bool rebalanceNeeded = RemoveRecursive<TK, TV>(root, key, comparer, owner, out countChanged);

            if (rebalanceNeeded)
            {
                if (!root.IsLeaf)
                {
                    var internalRoot = root.AsInternal();
                    if (internalRoot.Header.Count == 0)
                    {
                        return internalRoot.Children[0]!;
                    }
                }
            }

            return root;
        }

        private static bool RemoveRecursive<TK, TV>(Node<TK> node, TK key, IComparer<TK> comparer, OwnerId owner, out bool removed)
        {
            if (node.IsLeaf)
            {
                var leaf = node.AsLeaf<TV>();
                int index = FindIndex(leaf, key, comparer);

                if (index < leaf.Header.Count && comparer.Compare(leaf.Keys![index], key) == 0)
                {
                    RemoveFromLeaf(leaf, index);
                    removed = true;
                    return leaf.Header.Count < LeafNode<TK, TV>.MergeThreshold;
                }

                removed = false;
                return false;
            }
            else
            {
                var internalNode = node.AsInternal();
                int index = FindRoutingIndex(internalNode, key, comparer);

                var child = internalNode.Children[index]!.EnsureEditable(owner);
                internalNode.Children[index] = child;

                bool childUnderflow = RemoveRecursive<TK, TV>(child, key, comparer, owner, out removed);

                if (removed && childUnderflow)
                {
                    return HandleUnderflow<TK, TV>(internalNode, index, owner);
                }
                return false;
            }
        }

        // ---------------------------------------------------------
        // Internal Helpers: Search
        // ---------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FindIndex<TK,TV>(LeafNode<TK,TV> node, TK key, IComparer<TK> comparer)
        {
            if (typeof(TK) == typeof(int))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref int firstIntRef = ref Unsafe.As<TK, int>(ref firstKeyRef);
                ReadOnlySpan<int> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                int intKey = Unsafe.As<TK, int>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(uint))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref uint firstIntRef = ref Unsafe.As<TK, uint>(ref firstKeyRef);
                ReadOnlySpan<uint> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                uint intKey = Unsafe.As<TK, uint>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(long))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref long firstIntRef = ref Unsafe.As<TK, long>(ref firstKeyRef);
                ReadOnlySpan<long> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                long intKey = Unsafe.As<TK, long>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(ulong))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref ulong firstIntRef = ref Unsafe.As<TK, ulong>(ref firstKeyRef);
                ReadOnlySpan<ulong> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                ulong intKey = Unsafe.As<TK, ulong>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(short))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref short firstIntRef = ref Unsafe.As<TK, short>(ref firstKeyRef);
                ReadOnlySpan<short> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                short intKey = Unsafe.As<TK, short>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(ushort))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref ushort firstIntRef = ref Unsafe.As<TK, ushort>(ref firstKeyRef);
                ReadOnlySpan<ushort> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                ushort intKey = Unsafe.As<TK, ushort>(ref key);
                return Scanners.FindFirstGreaterOrEqualInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(float))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref float firstIntRef = ref Unsafe.As<TK, float>(ref firstKeyRef);
                ReadOnlySpan<float> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                float intKey = Unsafe.As<TK, float>(ref key);
                return Scanners.FindFirstGreaterOrEqualFloat(intKeys, intKey);
            }
            if (typeof(TK) == typeof(double))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref double firstIntRef = ref Unsafe.As<TK, double>(ref firstKeyRef);
                ReadOnlySpan<double> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                double intKey = Unsafe.As<TK, double>(ref key);
                return Scanners.FindFirstGreaterOrEqualFloat(intKeys, intKey);
            }

            return BinarySearchKeys(node.GetKeys(), key, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FindRoutingIndex<TK>(InternalNode<TK> node, TK key, IComparer<TK> comparer)
        {   
            if (typeof(TK) == typeof(int))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref int firstIntRef = ref Unsafe.As<TK, int>(ref firstKeyRef);
                ReadOnlySpan<int> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                int intKey = Unsafe.As<TK, int>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(uint))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref uint firstIntRef = ref Unsafe.As<TK, uint>(ref firstKeyRef);
                ReadOnlySpan<uint> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                uint intKey = Unsafe.As<TK, uint>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(long))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref long firstIntRef = ref Unsafe.As<TK, long>(ref firstKeyRef);
                ReadOnlySpan<long> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                long intKey = Unsafe.As<TK, long>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(ulong))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref ulong firstIntRef = ref Unsafe.As<TK, ulong>(ref firstKeyRef);
                ReadOnlySpan<ulong> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                ulong intKey = Unsafe.As<TK, ulong>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(short))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref short firstIntRef = ref Unsafe.As<TK, short>(ref firstKeyRef);
                ReadOnlySpan<short> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                short intKey = Unsafe.As<TK, short>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(ushort))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref ushort firstIntRef = ref Unsafe.As<TK, ushort>(ref firstKeyRef);
                ReadOnlySpan<ushort> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                ushort intKey = Unsafe.As<TK, ushort>(ref key);
                return Scanners.FindFirstGreaterInt(intKeys, intKey);
            }
            if (typeof(TK) == typeof(float))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref float firstIntRef = ref Unsafe.As<TK, float>(ref firstKeyRef);
                ReadOnlySpan<float> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                float intKey = Unsafe.As<TK, float>(ref key);
                return Scanners.FindFirstGreaterFloat(intKeys, intKey);
            }
            if (typeof(TK) == typeof(double))
            {
                Span<TK> keys = node.GetKeys();
                ref TK firstKeyRef = ref MemoryMarshal.GetReference(keys);
                ref double firstIntRef = ref Unsafe.As<TK, double>(ref firstKeyRef);
                ReadOnlySpan<double> intKeys = MemoryMarshal.CreateReadOnlySpan(ref firstIntRef, keys.Length);
                double intKey = Unsafe.As<TK, double>(ref key);
                return Scanners.FindFirstGreaterFloat(intKeys, intKey);
            }

            return BinaryRoutingKeys(node.GetKeys(), key, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BinarySearchKeys<TK>(ReadOnlySpan<TK> keys, TK key, IComparer<TK> comparer)
        {
            int low = 0;
            int high = keys.Length - 1;
            ref TK keysRef = ref MemoryMarshal.GetReference(keys);

            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                TK midKey = Unsafe.Add(ref keysRef, mid);
                int cmp = comparer.Compare(midKey, key);
        
                if (cmp == 0) return mid;
                if (cmp < 0) low = mid + 1;
                else high = mid - 1;
            }
            return low;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BinaryRoutingKeys<TK>(ReadOnlySpan<TK> keys, TK key, IComparer<TK> comparer)
        {
            int low = 0;
            int high = keys.Length - 1;
            ref TK keysRef = ref MemoryMarshal.GetReference(keys);
            
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                TK midKey = Unsafe.Add(ref keysRef, mid);
                int cmp = comparer.Compare(midKey, key);
                
                if (cmp <= 0) low = mid + 1;
                else high = mid - 1;
            }
            return low;
        }
        
        // ---------------------------------------------------------
        // Insertion Logic
        // ---------------------------------------------------------

        private static void InsertIntoLeaf<TK, TV>(LeafNode<TK, TV> leaf, int index, TK key, TV value)
        {
            int count = leaf.Header.Count;
            if (index < count)
            {
                int moveCount = count - index;
                leaf.Keys.AsSpan(index, moveCount).CopyTo(leaf.Keys.AsSpan(index + 1));
                leaf.Values.AsSpan(index, moveCount).CopyTo(leaf.Values.AsSpan(index + 1));
            }

            leaf.Keys![index] = key;
            leaf.Values[index] = value;
            leaf.SetCount(count + 1);
        }

        private static  (Node<TK>, TK) SplitLeaf<TK, TV>(LeafNode<TK, TV> left, int insertIndex, TK key, TV value, OwnerId owner)
        {
            var right = new LeafNode<TK, TV>(owner);
            int totalCount = left.Header.Count;

            int splitPoint = (insertIndex == totalCount) ? totalCount : (insertIndex == 0 ? 0 : totalCount / 2);
            int moveCount = totalCount - splitPoint;

            if (moveCount > 0)
            {
                left.Keys.AsSpan(splitPoint, moveCount).CopyTo(right.Keys.AsSpan(0));
                left.Values.AsSpan(splitPoint, moveCount).CopyTo(right.Values.AsSpan(0));
            }

            left.SetCount(splitPoint);
            right.SetCount(moveCount);

            if (insertIndex < splitPoint || (splitPoint == 0 && insertIndex == 0))
            {
                InsertIntoLeaf(left, insertIndex, key, value);
            }
            else
            {
                InsertIntoLeaf(right, insertIndex - splitPoint, key, value);
            }

            return (right, right.Keys![0]);
        }

        private static void InsertIntoInternal<TK>(InternalNode<TK> node, int index, TK separator, Node<TK> newChild)
        {
            int count = node.Header.Count;

            if (index < count)
            {
                int moveCount = count - index;
                Span<TK> keysSpan = node.Keys;
                keysSpan.Slice(index, moveCount).CopyTo(keysSpan.Slice(index + 1));
                
                Span<Node<TK>> childrenSpan = node.Children!;
                childrenSpan.Slice(index + 1, moveCount).CopyTo(childrenSpan.Slice(index + 2));
            }

            node.Keys[index] = separator;
            node.Children[index + 1] = newChild;
            node.SetCount(count + 1);
        }

        private static (Node<TK>, TK) SplitInternal<TK>(InternalNode<TK> left, int insertIndex, TK separator, Node<TK> newChild, OwnerId owner)
        {
            var right = new InternalNode<TK>(owner);
            
            int count = left.Header.Count;
            int splitPoint = count / 2;
            TK upKey = left.Keys[splitPoint];
            int moveCount = count - splitPoint - 1; 

            if (moveCount > 0)
            {
                Span<TK> leftKeys = left.Keys;
                Span<TK> rightKeys = right.Keys;
                leftKeys.Slice(splitPoint + 1, moveCount).CopyTo(rightKeys); 

                Span<Node<TK>> leftChildren = left.Children!;
                Span<Node<TK>> rightChildren = right.Children!;
                leftChildren.Slice(splitPoint + 1, moveCount + 1).CopyTo(rightChildren);
            }

            left.SetCount(splitPoint);
            right.SetCount(moveCount);

            if (insertIndex <= splitPoint)
            {
                InsertIntoInternal(left, insertIndex, separator, newChild);
            }
            else
            {
                InsertIntoInternal(right, insertIndex - (splitPoint + 1), separator, newChild);
            }

            return (right, upKey);
        }

        // ---------------------------------------------------------
        // Removal Logic
        // ---------------------------------------------------------

        private static void RemoveFromLeaf<TK, TV>(LeafNode<TK, TV> leaf, int index)
        {
            int count = leaf.Header.Count;
            int moveCount = count - index - 1;

            if (moveCount > 0)
            {
                leaf.Keys.AsSpan(index + 1, moveCount).CopyTo(leaf.Keys.AsSpan(index));
                leaf.Values.AsSpan(index + 1, moveCount).CopyTo(leaf.Values.AsSpan(index));
            }

            leaf.SetCount(count - 1);
        }

        private static bool HandleUnderflow<TK, TV>(InternalNode<TK> parent, int childIndex, OwnerId owner)
        {
            if (childIndex < parent.Header.Count)
            {
                var rightSibling = parent.Children[childIndex + 1]!.EnsureEditable(owner);
                parent.Children[childIndex + 1] = rightSibling;
                var leftChild = parent.Children[childIndex]!;

                if (CanBorrow(rightSibling))
                {
                    RotateLeft<TK, TV>(parent, childIndex, leftChild, rightSibling);
                    return false;
                }
                else
                {
                    Merge<TK, TV>(parent, childIndex, leftChild, rightSibling);
                    return parent.Header.Count < LeafNode<TK, TV>.MergeThreshold;
                }
            }
            else if (childIndex > 0)
            {
                var leftSibling = parent.Children[childIndex - 1]!.EnsureEditable(owner);
                parent.Children[childIndex - 1] = leftSibling;
                var rightChild = parent.Children[childIndex]!;

                if (CanBorrow(leftSibling))
                {
                    RotateRight<TK, TV>(parent, childIndex - 1, leftSibling, rightChild);
                    return false;
                }
                else
                {
                    Merge<TK, TV>(parent, childIndex - 1, leftSibling, rightChild);
                    return parent.Header.Count < LeafNode<TK, TV>.MergeThreshold;
                }
            }

            return true;
        }

        private static bool CanBorrow<TK>(Node<TK> node)
        {
            return node.Header.Count > 8 + 1;
        }

        private static void Merge<TK, TV>(InternalNode<TK> parent, int separatorIndex, Node<TK> left, Node<TK> right)
        {
            if (left.IsLeaf)
            {
                var leftLeaf = left.AsLeaf<TV>();
                var rightLeaf = right.AsLeaf<TV>();

                int lCount = leftLeaf.Header.Count;
                int rCount = rightLeaf.Header.Count;
                
                rightLeaf.Keys.AsSpan(0, rCount).CopyTo(leftLeaf.Keys.AsSpan(lCount));
                rightLeaf.Values.AsSpan(0, rCount).CopyTo(leftLeaf.Values.AsSpan(lCount));

                leftLeaf.SetCount(lCount + rCount);
            }
            else
            {
                var leftInternal = left.AsInternal();
                var rightInternal = right.AsInternal();

                TK separator = parent.Keys[separatorIndex];

                int lCount = leftInternal.Header.Count;
                leftInternal.Keys[lCount] = separator;

                int rCount = rightInternal.Header.Count;
                Span<TK> rightKeys = rightInternal.Keys;
                Span<TK> leftKeys = leftInternal.Keys;
                rightKeys.Slice(0, rCount).CopyTo(leftKeys.Slice(lCount + 1));

                Span<Node<TK>> rightChildren = rightInternal.Children!;
                Span<Node<TK>> leftChildren = leftInternal.Children!;
                rightChildren.Slice(0, rCount + 1).CopyTo(leftChildren.Slice(lCount + 1));

                leftInternal.SetCount(lCount + 1 + rCount);
            }

            int pCount = parent.Header.Count;
            int moveCount = pCount - separatorIndex - 1;

            if (moveCount > 0)
            {
                Span<TK> parentKeys = parent.Keys;
                parentKeys.Slice(separatorIndex + 1, moveCount).CopyTo(parentKeys.Slice(separatorIndex));

                Span<Node<TK>> parentChildren = parent.Children!;
                parentChildren.Slice(separatorIndex + 2, moveCount).CopyTo(parentChildren.Slice(separatorIndex + 1));
            }

            parent.SetCount(pCount - 1);
        }

        private static void RotateLeft<TK, TV>(InternalNode<TK> parent, int separatorIndex, Node<TK> left, Node<TK> right)
        {
            if (left.IsLeaf)
            {
                var leftLeaf = left.AsLeaf<TV>();
                var rightLeaf = right.AsLeaf<TV>();

                InsertIntoLeaf(leftLeaf, leftLeaf.Header.Count, rightLeaf.Keys![0], rightLeaf.Values[0]);
                RemoveFromLeaf(rightLeaf, 0);

                parent.Keys[separatorIndex] = rightLeaf.Keys[0];
            }
            else
            {
                var leftInternal = left.AsInternal();
                var rightInternal = right.AsInternal();

                TK sep = parent.Keys[separatorIndex];
                InsertIntoInternal(leftInternal, leftInternal.Header.Count, sep, rightInternal.Children[0]!);

                parent.Keys[separatorIndex] = rightInternal.Keys[0];

                int rCount = rightInternal.Header.Count;

                Span<Node<TK>> rightChildren = rightInternal.Children!;
                rightChildren.Slice(1, rCount).CopyTo(rightChildren);

                if (rCount > 1)
                {
                    Span<TK> rightKeys = rightInternal.Keys;
                    rightKeys.Slice(1, rCount - 1).CopyTo(rightKeys); 
                }

                rightInternal.SetCount(rCount - 1);
            }
        }

        private static void RotateRight<TK, TV>(InternalNode<TK> parent, int separatorIndex, Node<TK> left, Node<TK> right)
        {
            if (left.IsLeaf)
            {
                var leftLeaf = left.AsLeaf<TV>();
                var rightLeaf = right.AsLeaf<TV>();
                int last = leftLeaf.Header.Count - 1;

                InsertIntoLeaf(rightLeaf, 0, leftLeaf.Keys![last], leftLeaf.Values[last]);
                RemoveFromLeaf(leftLeaf, last);

                parent.Keys[separatorIndex] = rightLeaf.Keys![0];
            }
            else
            {
                var leftInternal = left.AsInternal();
                var rightInternal = right.AsInternal();
                int last = leftInternal.Header.Count - 1;

                TK sep = parent.Keys[separatorIndex];

                int rCount = rightInternal.Header.Count;

                // Shift keys right by 1
                Span<TK> rightKeys = rightInternal.Keys;
                rightKeys.Slice(0, rCount).CopyTo(rightKeys.Slice(1));
                rightKeys[0] = sep;

                // Shift children right by 1
                Span<Node<TK>> rightChildren = rightInternal.Children!;
                rightChildren.Slice(0, rCount + 1).CopyTo(rightChildren.Slice(1));
                rightChildren[0] = leftInternal.Children[last + 1]!;

                rightInternal.SetCount(rCount + 1);
                parent.Keys[separatorIndex] = leftInternal.Keys[last];
                leftInternal.SetCount(last);
            }
        }

        public static bool TryGetMin<TK, TV>(Node<TK> root, out TK key, out TV value)
        {
            var current = root;
            while (!current.IsLeaf)
            {
                current = current.AsInternal().Children[0]!;
            }

            var leaf = current.AsLeaf<TV>();
            if (leaf.Header.Count == 0)
            {
                key = default!;
                value = default!;
                return false;
            }

            key = leaf.Keys![0];
            value = leaf.Values[0];
            return true;
        }

        public static bool TryGetMax<TK, TV>(Node<TK> root, out TK key, out TV value)
        {
            var current = root;
            while (!current.IsLeaf)
            {
                var internalNode = current.AsInternal();
                current = internalNode.Children[internalNode.Header.Count]!;
            }

            var leaf = current.AsLeaf<TV>();
            if (leaf.Header.Count == 0)
            {
                key = default!;
                value = default!;
                return false;
            }

            int last = leaf.Header.Count - 1;
            key = leaf.Keys![last];
            value = leaf.Values[last];
            return true;
        }

        public static bool TryGetSuccessor<TK, TV>(Node<TK> root, TK key, IComparer<TK> comparer, out TK nextKey, out TV nextValue)
        {
            InternalNode<TK>[] path = new InternalNode<TK>[32];
            int[] indices = new int[32];
            int depth = 0;

            var current = root;
            while (!current.IsLeaf)
            {
                var internalNode = current.AsInternal();
                int idx = FindRoutingIndex(internalNode, key, comparer);
                path[depth] = internalNode;
                indices[depth] = idx;
                depth++;
                current = internalNode.Children[idx]!;
            }

            var leaf = current.AsLeaf<TV>();
            int index = FindIndex(leaf, key, comparer);

            if (index < leaf.Header.Count && comparer.Compare(leaf.Keys![index], key) == 0) index++;

            if (index < leaf.Header.Count)
            {
                nextKey = leaf.Keys![index];
                nextValue = leaf.Values[index];
                return true;
            }

            for (int i = depth - 1; i >= 0; i--)
            {
                if (indices[i] < path[i].Header.Count)
                {
                    current = path[i].Children[indices[i] + 1]!;
                    while (!current.IsLeaf)
                    {
                        current = current.AsInternal().Children[0]!;
                    }

                    var targetLeaf = current.AsLeaf<TV>();
                    nextKey = targetLeaf.Keys![0];
                    nextValue = targetLeaf.Values[0];
                    return true;
                }
            }

            nextKey = default!;
            nextValue = default!;
            return false;
        }

        public static bool TryGetPredecessor<TK, TV>(Node<TK> root, TK key, IComparer<TK> comparer, out TK prevKey, out TV prevValue)
        {
            InternalNode<TK>[] path = new InternalNode<TK>[32];
            int[] indices = new int[32];
            int depth = 0;

            var current = root;
            while (!current.IsLeaf)
            {
                var internalNode = current.AsInternal();
                int idx = FindRoutingIndex(internalNode, key, comparer);
                path[depth] = internalNode;
                indices[depth] = idx;
                depth++;
                current = internalNode.Children[idx]!;
            }

            var leaf = current.AsLeaf<TV>();
            int index = FindIndex(leaf, key, comparer);

            if (index > 0)
            {
                prevKey = leaf.Keys![index - 1];
                prevValue = leaf.Values[index - 1];
                return true;
            }

            for (int i = depth - 1; i >= 0; i--)
            {
                if (indices[i] > 0)
                {
                    current = path[i].Children[indices[i] - 1]!;
                    while (!current.IsLeaf)
                    {
                        var internalNode = current.AsInternal();
                        current = internalNode.Children[internalNode.Header.Count]!;
                    }

                    var targetLeaf = current.AsLeaf<TV>();
                    int last = targetLeaf.Header.Count - 1;
                    prevKey = targetLeaf.Keys![last];
                    prevValue = targetLeaf.Values[last];
                    return true;
                }
            }

            prevKey = default!;
            prevValue = default!;
            return false;
        }
    }
}
