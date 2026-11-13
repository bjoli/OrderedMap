namespace TestProject1;

using OrderedMap;

// OrderedMap.Concat: maps in key order make one map that holds each entry once, in order, and
// that changes like any map. The maps it took are not changed.
public class ConcatTests
{
    private static OrderedMap<int, int> Built(int n) =>
        OrderedMapModule.FromEnumerable(Enumerable.Range(0, n).Select(i => (i * 3, i * 6)), null);

    private static List<int> CursorKeys(OrderedMap<int, int> map)
    {
        var keys = new List<int>();
        for (var c = OrderedMapModule.Cursor(map); !OrderedMapModule.CursorDone(c); c = OrderedMapModule.CursorNext(c))
            keys.Add(OrderedMapModule.CursorCurrent(c).Item1);
        return keys;
    }

    // The map holds exactly `expected`, by every way to read it, and changes like a map.
    internal static void CheckMap(OrderedMap<int, int> map, List<(int Key, int Value)> expected)
    {
        Assert.Equal(expected.Count, map.Count);
        Assert.Equal(expected, map.Select(e => (e.Key, e.Value)));
        Assert.Equal(expected.Select(e => e.Key), CursorKeys(map));
        var keys = expected.Select(e => e.Key).ToHashSet();
        foreach (var (key, value) in expected.Where((_, i) => i % 13 == 0))
        {
            Assert.True(map.TryGetValue(key, out var v));
            Assert.Equal(value, v);
            Assert.Equal(keys.Contains(key + 1), map.ContainsKey(key + 1));
        }

        if (expected.Count > 0)
        {
            var mid = expected[expected.Count / 2].Key;
            Assert.Equal(expected.Where(e => e.Key >= mid).Select(e => e.Key), map.From(mid).Select(e => e.Key));
        }

        // Every other key removed, then all of them, then set again.
        var m = map;
        foreach (var (key, _) in expected.Where((_, i) => i % 2 == 0)) m = m.Remove(key);
        Assert.Equal(expected.Where((_, i) => i % 2 != 0).Select(e => e.Key), m.Select(e => e.Key));
        var t = map.ToTransient();
        foreach (var (key, _) in expected) t.Remove(key);
        Assert.Equal(0, t.Count);
        foreach (var (key, value) in expected) t.Set(key, value);
        Assert.Equal(expected, t.ToPersistent().Select(e => (e.Key, e.Value)));
        Assert.Equal(expected, map.Select(e => (e.Key, e.Value)));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(40, 4)]
    [InlineData(2000, 7)]
    [InlineData(100_000, 96)]
    [InlineData(300_000, 1000)]
    public void Concat_OfTheParts_IsTheMap(int n, int k)
    {
        var map = Built(n);
        var expected = map.Select(e => (e.Key, e.Value)).ToList();
        CheckMap(OrderedMap<int, int>.Concat(map.Split(k)), expected);
    }

    [Theory]
    [InlineData(100_000, 96, 2)]
    [InlineData(100_000, 96, 500)]
    [InlineData(100_000, 96, 100_000)]
    [InlineData(5000, 7, 3)]
    public void Concat_OfFilteredParts(int n, int k, int every)
    {
        var map = Built(n);
        var parts = map.Split(k).Select(p => OrderedMapModule.Filter((key, _) => key % every == 0, p)).ToArray();
        var expected = map.Where(e => e.Key % every == 0).Select(e => (e.Key, e.Value)).ToList();
        var joined = OrderedMap<int, int>.Concat(parts);
        CheckMap(joined, expected);
    }

    [Fact]
    public void Concat_OfMappedParts()
    {
        var map = Built(50_000);
        var parts = map.Split(96).Select(p => OrderedMapModule.MapValues(v => v + 1, p)).ToArray();
        CheckMap(OrderedMap<int, int>.Concat(parts), map.Select(e => (e.Key, e.Value + 1)).ToList());
    }

    [Fact]
    public void Concat_OfSmallMaps_JoinsTheirLeaves()
    {
        // 500 maps of two entries: few leaves, not 500.
        var maps = Enumerable.Range(0, 500).Select(i => Built(0).Set(i * 2, i).Set(i * 2 + 1, i)).ToArray();
        var joined = OrderedMap<int, int>.Concat(maps);
        CheckMap(joined, Enumerable.Range(0, 1000).Select(i => (i, i / 2)).ToList());
        Assert.True(OrderedMapModule.Split(joined, 1000).Length <= 1000 / 8);
    }

    [Fact]
    public void Concat_OfOneMap_IsIt()
    {
        var map = Built(100);
        Assert.Same(map, OrderedMap<int, int>.Concat([map]));
    }

    [Fact]
    public void Concat_OfEmptyMaps_IsEmpty()
    {
        var empty = OrderedMap<int, int>.Empty();
        var joined = OrderedMap<int, int>.Concat([empty, empty, empty]);
        Assert.Equal(0, joined.Count);
        Assert.Empty(joined);
        Assert.Equal(1, joined.Set(1, 1).Count);
    }

    [Fact]
    public void Concat_RejectsMapsNotInOrder()
    {
        Assert.Throws<ArgumentException>(() => OrderedMap<int, int>.Concat([Built(100), Built(100)]));
        Assert.Throws<ArgumentException>(() => OrderedMap<int, int>.Concat([]));
    }
}
