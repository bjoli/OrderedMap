using System;
using System.Collections.Generic;
using System.Linq;
using OrderedMap;
using Xunit;

/// <summary>
///     The struct cursor must give the entries the enumerator gives, in key order, for every
///     shape of tree: an empty root leaf, a full root leaf, several levels, and leaves that
///     removals left small.
/// </summary>
public class CursorTests
{
    private static List<(TK, TV)> Walk<TK, TV>(OrderedMap<TK, TV> map)
    {
        var seen = new List<(TK, TV)>();
        for (var c = OrderedMapModule.Cursor(map); !OrderedMapModule.CursorDone(c); c = OrderedMapModule.CursorNext(c))
            seen.Add(OrderedMapModule.CursorCurrent(c));
        return seen;
    }

    private static void AssertSameWalk<TK, TV>(OrderedMap<TK, TV> map)
    {
        var walked = Walk(map);
        Assert.Equal(map.Select(kv => (kv.Key, kv.Value)).ToList(), walked);
        Assert.Equal(map.Count, walked.Count);
    }

    private static OrderedMap<int, int> Ints(IEnumerable<int> keys)
    {
        var map = BaseOrderedMap<int, int>.CreateTransient();
        foreach (var k in keys) map.Set(k, k * 10);
        return map.ToPersistent();
    }

    [Fact]
    public void Empty()
    {
        Assert.True(OrderedMapModule.CursorDone(OrderedMapModule.Cursor(OrderedMap<int, int>.Empty(null))));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(1000)]
    [InlineData(200_000)]
    public void InKeyOrder(int n)
    {
        // Shuffled, so the leaves are not all full.
        var keys = Enumerable.Range(0, n).OrderBy(k => (k * 7919) % 10007).ToArray();
        var map = Ints(keys);
        AssertSameWalk(map);
        Assert.Equal(Enumerable.Range(0, n), Walk(map).Select(e => e.Item1));
    }

    [Fact]
    public void Persistent()
    {
        var map = OrderedMap<string, int>.Empty(null);
        for (var i = 0; i < 5000; i++) map = map.Set("k" + i, i);
        AssertSameWalk(map);
    }

    [Fact]
    public void AfterRemovals()
    {
        var map = Ints(Enumerable.Range(0, 20_000));
        for (var i = 0; i < 20_000; i += 3) map = map.Remove(i);
        AssertSameWalk(map);
        for (var i = 1; i < 20_000; i += 2) map = map.Remove(i);
        AssertSameWalk(map);
        foreach (var k in map.Select(kv => kv.Key).ToList()) map = map.Remove(k);
        Assert.True(OrderedMapModule.CursorDone(OrderedMapModule.Cursor(map)));
    }

    [Fact]
    public void ACursorIsAValue()
    {
        var map = Ints(Enumerable.Range(0, 500));
        var first = OrderedMapModule.Cursor(map);
        var c = first;
        for (var i = 0; i < 100; i++) c = OrderedMapModule.CursorNext(c);

        Assert.Equal((0, 0), OrderedMapModule.CursorCurrent(first));
        Assert.Equal((100, 1000), OrderedMapModule.CursorCurrent(c));
    }
}
