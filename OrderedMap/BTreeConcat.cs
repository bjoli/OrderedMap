/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Copyright (c) 2026 Linus Björnstam
 *
 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OrderedMap;

// Joins trees in key order into one tree, for (std parallel)'s join of the parts of a map. The
// leaves are kept, and new internal nodes are made over them, as BTreeSplit.Over makes them.
//
// - The leaves are found by a descent of each tree that visits its internal nodes only.
// - An empty leaf is dropped. A leaf below MergeThreshold is copied together with its
//   neighbour when the two fit in one leaf, so that the parts of a filter that kept little
//   make few small leaves.
// - Each tree must have only keys less than the next tree's first key. That is checked at the
//   trees' edges, one compare for each tree.
[NonUserCode]
internal static class BTreeConcat<TK, TV>
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static (Node<TK> Root, int Count) Concat(Node<TK>[] roots, IComparer<TK> comparer)
    {
        var leaves = new List<Node<TK>>();
        foreach (var root in roots)
        {
            var first = leaves.Count;
            Collect(root, leaves);
            if (leaves.Count == first || first == 0) continue;

            var before = leaves[first - 1].AsLeaf<TV>();
            var after = leaves[first].AsLeaf<TV>();
            if (comparer.Compare(before.Keys![before.Header.Count - 1], after.Keys![0]) >= 0)
                throw new ArgumentException("The maps are not in key order: a key of one is not less than every key of the next.");
        }

        if (leaves.Count == 0) return (new LeafNode<TK, TV>(OwnerId.None), 0);

        var joined = new List<Node<TK>>(leaves.Count);
        var count = 0;
        foreach (var node in leaves)
        {
            var leaf = node.AsLeaf<TV>();
            count += leaf.Header.Count;
            if (joined.Count > 0)
            {
                var last = joined[^1].AsLeaf<TV>();
                var small = last.Header.Count < LeafNode<TK, TV>.MergeThreshold
                            || leaf.Header.Count < LeafNode<TK, TV>.MergeThreshold;
                if (small && last.Header.Count + leaf.Header.Count <= LeafNode<TK, TV>.Capacity)
                {
                    joined[^1] = Both(last, leaf);
                    continue;
                }
            }

            joined.Add(leaf);
        }

        return (BTreeSplit<TK>.Over(joined), count);
    }

    // The leaves under a node, in order, but for empty ones.
    private static void Collect(Node<TK> node, List<Node<TK>> leaves)
    {
        if (node.IsLeaf)
        {
            if (node.Header.Count > 0) leaves.Add(node);
            return;
        }

        foreach (var child in node.AsInternal().GetChildren()) Collect(child, leaves);
    }

    // A new leaf with the entries of a and then those of b.
    private static LeafNode<TK, TV> Both(LeafNode<TK, TV> a, LeafNode<TK, TV> b)
    {
        var leaf = new LeafNode<TK, TV>(OwnerId.None);
        int na = a.Header.Count, nb = b.Header.Count;
        Array.Copy(a.Keys!, 0, leaf.Keys!, 0, na);
        Array.Copy(a.Values, 0, leaf.Values, 0, na);
        Array.Copy(b.Keys!, 0, leaf.Keys!, na, nb);
        Array.Copy(b.Values, 0, leaf.Values, na, nb);
        leaf.SetCount(na + nb);
        return leaf;
    }
}
