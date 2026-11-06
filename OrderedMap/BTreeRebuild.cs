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

// Map and Filter by the tree's leaves, with no list of all the entries. A builder of the map's
// size keeps such a list, and for a map of more than about 5000 entries it is in the large
// object heap, which made the parts of (std parallel)'s par-map no faster on 24 threads than one
// after the other.
//
// - Map gives a tree of the same shape: each leaf and internal node is copied, with new values.
// - Filter fills new leaves one after the other with the entries it keeps, and makes internal
//   nodes over them, as BTreeSplit.Over does. When it keeps every entry, it returns the map.
// - Both visit the entries in key order, so the function is called in that order.
[NonUserCode]
internal static class BTreeRebuild<TK, TV>
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Node<TK> Map<TV2>(Node<TK> node, Func<TK, TV, TV2> mapper)
    {
        if (node.IsLeaf)
        {
            var leaf = node.AsLeaf<TV>();
            var n = leaf.Header.Count;
            var copy = new LeafNode<TK, TV2>(OwnerId.None);
            Array.Copy(leaf.Keys!, copy.Keys!, n);
            var keys = leaf.Keys!;
            var values = leaf.Values;
            var into = copy.Values;
            for (var i = 0; i < n; i++) into[i] = mapper(keys[i], values[i]);
            copy.SetCount(n);
            return copy;
        }

        var inner = node.AsInternal();
        var parent = new InternalNode<TK>(OwnerId.None) { Keys = inner.Keys };
        parent.SetCount(inner.Header.Count);
        var children = inner.GetChildren();
        var mapped = parent.GetChildren();
        for (var i = 0; i < children.Length; i++) mapped[i] = Map(children[i], mapper);
        return parent;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Node<TK> MapValues<TV2>(Node<TK> node, Func<TV, TV2> mapper)
    {
        if (node.IsLeaf)
        {
            var leaf = node.AsLeaf<TV>();
            var n = leaf.Header.Count;
            var copy = new LeafNode<TK, TV2>(OwnerId.None);
            Array.Copy(leaf.Keys!, copy.Keys!, n);
            var values = leaf.Values;
            var into = copy.Values;
            for (var i = 0; i < n; i++) into[i] = mapper(values[i]);
            copy.SetCount(n);
            return copy;
        }

        var inner = node.AsInternal();
        var parent = new InternalNode<TK>(OwnerId.None) { Keys = inner.Keys };
        parent.SetCount(inner.Header.Count);
        var children = inner.GetChildren();
        var mapped = parent.GetChildren();
        for (var i = 0; i < children.Length; i++) mapped[i] = MapValues(children[i], mapper);
        return parent;
    }

    /// <summary>The kept entries' tree and their number, or null when every entry is kept.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static (Node<TK>? Root, int Count) Filter(Node<TK> root, Func<TK, TV, bool> keep)
    {
        var filter = new Filtering(keep);
        filter.Visit(root);
        if (!filter.Dropped) return (null, filter.Count);
        if (filter.Current.Header.Count > 0 || filter.Leaves.Count == 0) filter.Leaves.Add(filter.Current);
        return (BTreeSplit<TK>.Over(filter.Leaves), filter.Count);
    }

    [NonUserCode]
    private sealed class Filtering(Func<TK, TV, bool> keep)
    {
        public readonly List<Node<TK>> Leaves = new();
        public LeafNode<TK, TV> Current = new(OwnerId.None);
        public int Count;
        public bool Dropped;

        public void Visit(Node<TK> node)
        {
            if (!node.IsLeaf)
            {
                foreach (var child in node.AsInternal().GetChildren()) Visit(child);
                return;
            }

            var leaf = node.AsLeaf<TV>();
            var keys = leaf.Keys!;
            var values = leaf.Values;
            for (var i = 0; i < leaf.Header.Count; i++)
            {
                if (!keep(keys[i], values[i]))
                {
                    Dropped = true;
                    continue;
                }

                var at = Current.Header.Count;
                if (at == LeafNode<TK, TV>.Capacity)
                {
                    Leaves.Add(Current);
                    Current = new LeafNode<TK, TV>(OwnerId.None);
                    at = 0;
                }

                Current.Keys![at] = keys[i];
                Current.Values[at] = values[i];
                Current.SetCount(at + 1);
                Count++;
            }
        }
    }
}
