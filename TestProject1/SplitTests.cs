namespace TestProject1;

using OrderedMap;

// OrderedMap.Split: the parts hold each entry once in key order, are maps of their own, and
// leave the map as it was.
public class SplitTests
{
    private static OrderedMap<int, int> Built(int n) =>
        OrderedMapModule.FromEnumerable(Enumerable.Range(0, n).Select(i => (i * 3, i * 6)), null);

    // Inserted in a shuffled order and thinned out, so nodes are not full and not equal.
    private static (OrderedMap<int, int> Map, int[] Keys) Grown(int n, int seed)
    {
        var rng = new Random(seed);
        var map = OrderedMap<int, int>.Empty();
        var keys = Enumerable.Range(0, n).Select(i => i * 3).OrderBy(_ => rng.Next()).ToArray();
        foreach (var key in keys) map = map.Set(key, key * 2);
        foreach (var key in keys.Where((_, i) => i % 3 == 0)) map = map.Remove(key);
        return (map, map.Select(e => e.Key).ToArray());
    }

    private static List<int> CursorKeys(OrderedMap<int, int> map)
    {
        var keys = new List<int>();
        for (var c = OrderedMapModule.Cursor(map); !OrderedMapModule.CursorDone(c); c = OrderedMapModule.CursorNext(c))
            keys.Add(OrderedMapModule.CursorCurrent(c).Item1);
        return keys;
    }

    private static void CheckParts(OrderedMap<int, int> map, int k)
    {
        var all = map.Select(e => e.Key).ToList();
        var parts = map.Split(k);
        Assert.InRange(parts.Length, 1, Math.Max(1, k));

        var joined = new List<int>();
        foreach (var part in parts)
        {
            var keys = part.Select(e => e.Key).ToList();
            Assert.Equal(keys.Count, part.Count);
            Assert.Equal(keys, CursorKeys(part));
            foreach (var e in part) Assert.Equal(e.Key * 2, e.Value);
            foreach (var key in keys.Where((_, i) => i % 17 == 0))
            {
                Assert.True(part.TryGetValue(key, out var v));
                Assert.Equal(key * 2, v);
                Assert.False(part.ContainsKey(key + 1));
            }

            if (keys.Count > 0)
            {
                Assert.True(part.TryGetMin(out var min, out _));
                Assert.True(part.TryGetMax(out var max, out _));
                Assert.Equal(keys[0], min);
                Assert.Equal(keys[^1], max);
                Assert.Equal(keys.Where(x => x >= keys[keys.Count / 2]), part.From(keys[keys.Count / 2]).Select(e => e.Key));
            }

            if (joined.Count > 0 && keys.Count > 0) Assert.True(joined[^1] < keys[0]);
            joined.AddRange(keys);
        }

        Assert.Equal(all, joined);

        // A key of another part is not found.
        if (parts.Length > 1)
        {
            var first = parts[0].Select(e => e.Key).First();
            Assert.False(parts[1].ContainsKey(first));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(2000)]
    [InlineData(5000)]
    [InlineData(100_000)]
    [InlineData(300_000)]
    public void Split_PartsHoldEachEntryOnceInOrder(int n)
    {
        var map = Built(n);
        foreach (var k in new[] { 1, 2, 3, 7, 96, 1000 }) CheckParts(map, k);
    }

    [Theory]
    [InlineData(3000, 1)]
    [InlineData(50_000, 2)]
    [InlineData(200_000, 3)]
    public void Split_OfAGrownTree(int n, int seed)
    {
        var (map, _) = Grown(n, seed);
        foreach (var k in new[] { 2, 5, 96, 1000 }) CheckParts(map, k);
    }

    [Fact]
    public void Split_PartsAreClose()
    {
        var map = Built(300_000);
        foreach (var k in new[] { 2, 8, 96 })
        {
            var parts = map.Split(k);
            Assert.Equal(k, parts.Length);
            Assert.All(parts, p => Assert.InRange(p.Count, 300_000 / k / 2, 300_000 / k * 2));
        }
    }

    [Fact]
    public void Split_OneOrALeafIsTheMap()
    {
        var map = Built(1000);
        Assert.Same(map, Assert.Single(map.Split(1)));
        Assert.Same(map, Assert.Single(map.Split(0)));
        var small = Built(40);
        Assert.Same(small, Assert.Single(small.Split(8)));
        var empty = OrderedMap<int, int>.Empty();
        Assert.Same(empty, Assert.Single(empty.Split(8)));
    }

    [Fact]
    public void Split_PartsChangeLikeMaps()
    {
        var (map, keys) = Grown(60_000, 7);
        var parts = map.Split(16);
        Assert.True(parts.Length > 1);
        foreach (var part in parts)
        {
            var own = part.Select(e => e.Key).ToArray();
            var p = part;
            foreach (var key in own.Where((_, i) => i % 2 == 0)) p = p.Remove(key);
            p = p.Set(-1, -2).Set(int.MaxValue, 0);
            var expected = own.Where((_, i) => i % 2 != 0).Prepend(-1).Append(int.MaxValue).ToList();
            Assert.Equal(expected, p.Select(e => e.Key));
            Assert.Equal(expected.Count, p.Count);

            // Down to nothing and back, in a transient.
            var t = part.ToTransient();
            foreach (var key in own) t.Remove(key);
            Assert.Equal(0, t.Count);
            foreach (var key in own) t.Set(key, 1);
            Assert.Equal(own, t.ToPersistent().Select(e => e.Key));
            Assert.Equal(own, part.Select(e => e.Key));
        }

        Assert.Equal(keys, map.Select(e => e.Key));
    }
}
