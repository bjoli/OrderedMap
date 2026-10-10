using System;
using System.Runtime.CompilerServices;

namespace OrderedMap;

/// <summary>
///     A position in a walk of an <see cref="OrderedMap{TK, TV}" />, in key order.
///
///     A struct, and <see cref="Next" /> answers the advanced cursor, so a walk allocates
///     nothing. That matters for the short walks a loop starts many times.
///
///     The cursor holds the key and value arrays of one leaf, so a step in a leaf is an
///     increment and a compare. At the end of a leaf, <see cref="Advance" /> finds the next
///     leaf, out of line. The nodes have no parent links, so the cursor keeps the path to its
///     leaf: the child index at each level, packed in a <c>ulong</c>. <see cref="Advance" />
///     follows the path from the root again. The tree is a few levels deep, and a leaf holds
///     many entries, so this costs little for each entry.
/// </summary>
public readonly struct OrderedMapCursor<TK, TV>
{
    // The depth of the leaf is in the low bits of the path, and the child index at each
    // level above it in 6 bits per level, as an internal node has up to 32 children.
    private const int DepthBits = 4;
    private const ulong DepthMask = (1UL << DepthBits) - 1;
    private const int IndexBits = 6;
    private const int MaxDepth = 10;

    // The path of a leaf with no leaf after it. Not a path: its depth is more than MaxDepth.
    private const ulong LastLeaf = ulong.MaxValue;

    private readonly TK[]? _keys;
    private readonly TV[]? _values;
    private readonly int _i;
    private readonly int _end;
    private readonly Node<TK>? _root;
    private readonly ulong _path;

    private OrderedMapCursor(TK[] keys, TV[] values, int i, int end, Node<TK> root, ulong path)
    {
        _keys = keys;
        _values = values;
        _i = i;
        _end = end;
        _root = root;
        _path = path;
    }

    /// <summary>
    ///     The cursor on the first entry. Inlined where a walk starts, so the walk of a map
    ///     whose root is a leaf makes no call. A map of up to 64 entries is one leaf.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OrderedMapCursor<TK, TV> Start(Node<TK> root)
    {
        if (!root.IsLeaf) return StartAtInternal(root);
        var leaf = root.AsLeaf<TV>();
        return new OrderedMapCursor<TK, TV>(leaf.Keys!, leaf.Values, 0, leaf.Header.Count, root, LastLeaf);
    }

    public bool Done
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _i >= _end;
    }

    /// <summary>The entry the cursor is on. Only when not <see cref="Done" />.</summary>
    public (TK, TV) Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_keys![_i], _values![_i]);
    }

    // The slow path takes and answers values. A method called on the cursor itself would
    // take its address, and the JIT then keeps the whole cursor in memory rather than in
    // registers.
    /// <summary>The cursor on the next entry. Only when not <see cref="Done" />.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OrderedMapCursor<TK, TV> Next()
    {
        var i = _i + 1;
        if (i < _end || _path == LastLeaf)
            return new OrderedMapCursor<TK, TV>(_keys!, _values!, i, _end, _root!, _path);
        return Advance(_root!, _path);
    }

    private static int IndexAt(ulong path, int level) =>
        (int)(path >> (DepthBits + IndexBits * level)) & ((1 << IndexBits) - 1);

    private static ulong WithIndex(ulong path, int level, int index)
    {
        var shift = DepthBits + IndexBits * level;
        return (path & ~(((1UL << IndexBits) - 1) << shift)) | ((ulong)index << shift);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OrderedMapCursor<TK, TV> StartAtInternal(Node<TK> root)
    {
        var nodes = new PathNodes();
        return Descend(root, root, 0, 0, ref nodes);
    }

    // The cursor on the first entry of the leftmost leaf under `node`, which is at `level`,
    // or on the first entry after it if that leaf is empty.
    private static OrderedMapCursor<TK, TV> Descend(Node<TK> node, Node<TK> root, int level, ulong path,
        ref PathNodes nodes)
    {
        while (!node.IsLeaf)
        {
            if (level == MaxDepth) throw new InvalidOperationException("The map is deeper than a cursor can walk.");
            nodes[level] = node;
            path = WithIndex(path, level, 0);
            level++;
            node = node.AsInternal().Children[0]!;
        }

        path = (path & ~DepthMask) | (ulong)level;
        var leaf = node.AsLeaf<TV>();
        if (leaf.Header.Count > 0)
            return new OrderedMapCursor<TK, TV>(leaf.Keys!, leaf.Values, 0, leaf.Header.Count, root, path);
        return NextLeaf(root, path, level, ref nodes);
    }

    // The cursor on the first entry of the leaf after the leaf at `path`, or a done cursor.
    // `nodes` holds the internal nodes on the path above `depth`.
    private static OrderedMapCursor<TK, TV> NextLeaf(Node<TK> root, ulong path, int depth, ref PathNodes nodes)
    {
        var level = depth;
        while (level > 0)
        {
            level--;
            var parent = nodes[level].AsInternal();
            var next = IndexAt(path, level) + 1;
            if (next <= parent.Header.Count)
                return Descend(parent.Children[next]!, root, level + 1, WithIndex(path, level, next), ref nodes);
        }

        return default;
    }

    /// <summary>The cursor on the first entry after the leaf at <paramref name="path" />.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OrderedMapCursor<TK, TV> Advance(Node<TK> root, ulong path)
    {
        var nodes = new PathNodes();
        var depth = (int)(path & DepthMask);
        Node<TK> node = root;
        for (var level = 0; level < depth; level++)
        {
            nodes[level] = node;
            node = node.AsInternal().Children[IndexAt(path, level)]!;
        }

        return NextLeaf(root, path, depth, ref nodes);
    }

    [InlineArray(MaxDepth)]
    private struct PathNodes
    {
        private Node<TK> _element0;
    }
}
