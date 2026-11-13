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
using System.Runtime.InteropServices;

namespace OrderedMap;

// Splits a tree into about k trees, in key order, that together hold each key once, for work
// on the parts at the same time. The parts share the tree's nodes: a part is a run of the nodes
// of one level, under new internal nodes when the run has more than one. The keys are not
// counted, which would visit every leaf: a part is counted when its count is asked for.
//
// - The level is the highest one with at least k nodes, or the leaves.
// - A node of the level weighs as many leaves as it has below it, which the internal nodes
//   above the leaves say. A node of the leaf level weighs its keys.
// - The runs are cut where the running weight passes i * total / k, so the parts are close in
//   size, not equal: a node is not cut, and leaves are not equally full.
// - A new internal node's separators are the first keys of its children after the first, as
//   the builder makes them. Every leaf of the run is at the same depth, so the part is a tree
//   of the same shape rules as the one it came from.
//
// The split runs once for each call of in-parallel, so its code would stay unoptimised under
// tiered compilation: AggressiveOptimization compiles it optimised at the first call.
[NonUserCode]
internal static class BTreeSplit<TK>
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Node<TK>[] Split(Node<TK> root, int k)
    {
        if (k <= 1 || root.IsLeaf) return [root];

        var level = new List<Node<TK>> { root };
        while (level.Count < k && !level[0].IsLeaf)
        {
            var next = new List<Node<TK>>(level.Count * InternalNode<TK>.Capacity);
            foreach (var node in level) next.AddRange(node.AsInternal().GetChildren());
            level = next;
        }

        if (level.Count == 1) return [root];

        var weights = new long[level.Count];
        long total = 0;
        for (var i = 0; i < level.Count; i++) total += weights[i] = Weight(level[i]);

        var parts = Math.Min(k, level.Count);
        var result = new List<Node<TK>>(parts);
        var start = 0;
        long done = 0;
        for (var j = 0; j < level.Count; j++)
        {
            done += weights[j];
            var last = j == level.Count - 1;
            if (!last && (result.Count >= parts - 1 || done < total * (result.Count + 1) / parts)) continue;
            result.Add(Over(level.GetRange(start, j + 1 - start)));
            start = j + 1;
        }

        return result.ToArray();
    }

    // One tree over nodes of one depth, in order: internal nodes of up to 32 children each, as
    // even as they can be, level by level up to one root.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static Node<TK> Over(List<Node<TK>> nodes)
    {
        while (nodes.Count > 1)
        {
            var parents = (nodes.Count + InternalNode<TK>.Capacity - 1) / InternalNode<TK>.Capacity;
            var up = new List<Node<TK>>(parents);
            var offset = 0;
            for (var p = 0; p < parents; p++)
            {
                var n = nodes.Count / parents + (p < nodes.Count % parents ? 1 : 0);
                var parent = new InternalNode<TK>(OwnerId.None);
                var children = MemoryMarshal.CreateSpan(ref parent.Children[0]!, n);
                var keys = MemoryMarshal.CreateSpan(ref parent.Keys[0], n - 1);
                for (var c = 0; c < n; c++)
                {
                    children[c] = nodes[offset + c];
                    if (c > 0) keys[c - 1] = FirstKey(nodes[offset + c]);
                }

                parent.SetCount(n - 1);
                up.Add(parent);
                offset += n;
            }

            nodes = up;
        }

        return nodes[0];
    }

    private static TK FirstKey(Node<TK> node)
    {
        while (!node.IsLeaf) node = node.AsInternal().GetChildren()[0];
        return node.GetKeys()[0];
    }

    // The leaves under an internal node, or the keys of a leaf. Visits the internal nodes only.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static long Weight(Node<TK> node)
    {
        if (node.IsLeaf) return node.Header.Count;
        var children = node.AsInternal().GetChildren();
        if (children[0].IsLeaf) return children.Length;
        long n = 0;
        foreach (var child in children) n += Weight(child);
        return n;
    }

    // The keys under a node: the sum of its leaves' counts. A walk of every leaf: the count of
    // a part made by Split.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static int CountKeys(Node<TK> node)
    {
        if (node.IsLeaf) return node.Header.Count;
        var n = 0;
        foreach (var child in node.AsInternal().GetChildren()) n += CountKeys(child);
        return n;
    }
}
