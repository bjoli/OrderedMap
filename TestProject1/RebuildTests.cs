namespace TestProject1;

using OrderedMap;

// Map, MapValues and Filter, which rebuild the tree by its leaves: the result holds the right
// entries in key order, changes like any map, and leaves the map as it was.
public class RebuildTests
{
    // Inserted in a shuffled order and thinned out, so nodes are not full and not equal.
    private static OrderedMap<int, int> Grown(int n, int seed)
    {
        var rng = new Random(seed);
        var map = OrderedMap<int, int>.Empty();
        var keys = Enumerable.Range(0, n).Select(i => i * 3).OrderBy(_ => rng.Next()).ToArray();
        foreach (var key in keys) map = map.Set(key, key * 2);
        foreach (var key in keys.Where((_, i) => i % 3 == 0)) map = map.Remove(key);
        return map;
    }

    public static IEnumerable<object[]> Sizes() =>
        new[] { 0, 1, 63, 64, 65, 2000, 100_000 }.SelectMany(n => new[] { new object[] { n, 1 }, [n, 2] });

    [Theory]
    [MemberData(nameof(Sizes))]
    public void MapValues_KeepsKeysAndOrder(int n, int seed)
    {
        var map = Grown(n, seed);
        var before = map.Select(e => (e.Key, e.Value)).ToList();
        var mapped = OrderedMapModule.MapValues(v => (long)v + 1, map);
        Assert.Equal(before.Select(e => (e.Key, (long)e.Value + 1)), mapped.Select(e => (e.Key, e.Value)));
        Assert.Equal(before.Count, mapped.Count);
        ConcatTests.CheckMap(OrderedMapModule.MapValues(v => v + 1, map), before.Select(e => (e.Key, e.Value + 1)).ToList());
        Assert.Equal(before, map.Select(e => (e.Key, e.Value)));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Map_GivesTheKey(int n, int seed)
    {
        var map = Grown(n, seed);
        var before = map.Select(e => (e.Key, e.Value)).ToList();
        ConcatTests.CheckMap(OrderedMapModule.Map((k, v) => k + v, map), before.Select(e => (e.Key, e.Key + e.Value)).ToList());
        Assert.Equal(before, map.Select(e => (e.Key, e.Value)));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Filter_KeepsTheEntriesInOrder(int n, int seed)
    {
        var map = Grown(n, seed);
        var before = map.Select(e => (e.Key, e.Value)).ToList();
        foreach (var every in new[] { 2, 7, 100, 1_000_000 })
        {
            var kept = OrderedMapModule.Filter((k, _) => k % every == 0, map);
            ConcatTests.CheckMap(kept, before.Where(e => e.Key % every == 0).ToList());
        }

        Assert.Equal(before, map.Select(e => (e.Key, e.Value)));
    }

    [Fact]
    public void Filter_ThatKeepsAll_IsTheMap()
    {
        var map = Grown(5000, 3);
        Assert.Same(map, OrderedMapModule.Filter((_, _) => true, map));
    }

    [Fact]
    public void Filter_ThatKeepsNone_IsEmpty()
    {
        var kept = OrderedMapModule.Filter((_, _) => false, Grown(5000, 3));
        Assert.Equal(0, kept.Count);
        Assert.Empty(kept);
        Assert.Equal(1, kept.Set(1, 1).Count);
    }

    [Fact]
    public void Rebuild_CallsTheFunctionInKeyOrder()
    {
        var map = Grown(10_000, 5);
        var seen = new List<int>();
        OrderedMapModule.Map((k, v) => { seen.Add(k); return v; }, map);
        Assert.Equal(map.Select(e => e.Key), seen);
        seen.Clear();
        OrderedMapModule.Filter((k, _) => { seen.Add(k); return k % 2 == 0; }, map);
        Assert.Equal(map.Select(e => e.Key), seen);
    }
}
