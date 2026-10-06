using System;
using System.Linq;
using Xunit;
using OrderedMap;

namespace TestProject1;

// The vector scans take a different path with and without AVX-512. Run these once more
// under DOTNET_EnableAVX512=0 to cover the 256-bit one.
public class KeyEdgeTests
{
    // Enough keys that every leaf scan has a full vector to load.
    private static OrderedMap<T, string> WithSmallest<T>(T smallest, Func<int, T> key)
    {
        var map = OrderedMap<T, string>.Empty().Set(smallest, "smallest");
        for (int i = 1; i <= 40; i++) map = map.Set(key(i), i.ToString());
        return map;
    }

    [Fact]
    public void TheSmallestValueOfEachIntegerTypeIsFound()
    {
        Assert.True(WithSmallest(int.MinValue, i => i).ContainsKey(int.MinValue));
        Assert.True(WithSmallest(long.MinValue, i => (long)i).ContainsKey(long.MinValue));
        Assert.True(WithSmallest(short.MinValue, i => (short)i).ContainsKey(short.MinValue));
        Assert.True(WithSmallest(0u, i => (uint)i).ContainsKey(0u));
        Assert.True(WithSmallest(0ul, i => (ulong)i).ContainsKey(0ul));
        Assert.True(WithSmallest((ushort)0, i => (ushort)i).ContainsKey((ushort)0));
    }

    [Fact]
    public void SettingTheSmallestValueAgainReplacesIt()
    {
        var map = WithSmallest(0u, i => (uint)i).Set(0u, "again");
        Assert.Equal(41, map.Count);
        Assert.True(map.TryGetValue(0u, out var v));
        Assert.Equal("again", v);
    }

    [Fact]
    public void NaNIsOneKeyAndIsFound()
    {
        var map = OrderedMap<double, string>.Empty();
        for (int i = 0; i < 40; i++) map = map.Set(i, i.ToString());
        map = map.Set(double.NaN, "nan").Set(double.NaN, "nan again");

        Assert.Equal(41, map.Count);
        Assert.True(map.TryGetValue(double.NaN, out var v));
        Assert.Equal("nan again", v);
        Assert.Equal(double.NaN, map.First().Key);
        Assert.True(map.ContainsKey(5.0));

        map = map.Remove(double.NaN);
        Assert.Equal(40, map.Count);
        Assert.False(map.ContainsKey(double.NaN));
    }

    [Fact]
    public void FloatNaNIsOneKeyAndIsFound()
    {
        var map = OrderedMap<float, int>.Empty();
        for (int i = 0; i < 40; i++) map = map.Set(i, i);
        map = map.Set(float.NaN, -1).Set(float.NaN, -2);

        Assert.Equal(41, map.Count);
        Assert.True(map.TryGetValue(float.NaN, out var v));
        Assert.Equal(-2, v);
    }
}

public class BuilderOrderTests
{
    [Fact]
    public void AscendingInputBuildsTheSameMapAsShuffledInput()
    {
        var keys = Enumerable.Range(0, 5000).ToArray();
        var ascending = new OrderedMapBuilder<int, int>();
        foreach (var k in keys) ascending.Add(k, k * 2);

        var shuffled = new OrderedMapBuilder<int, int>();
        foreach (var k in keys.OrderBy(k => (k * 7919) % 5003)) shuffled.Add(k, k * 2);

        var a = ascending.Build();
        var b = shuffled.Build();
        Assert.Equal(5000, a.Count);
        Assert.Equal(a, b);
        Assert.Equal(keys, a.Select(kv => kv.Key));
    }

    [Fact]
    public void ARepeatedKeyAtTheEndStillWins()
    {
        var builder = new OrderedMapBuilder<int, string>();
        for (int i = 0; i < 100; i++) builder.Add(i, "first");
        builder.Add(99, "last");

        var map = builder.Build();
        Assert.Equal(100, map.Count);
        Assert.True(map.TryGetValue(99, out var v));
        Assert.Equal("last", v);
    }

    [Fact]
    public void MappingAMapKeepsEveryEntry()
    {
        var builder = new OrderedMapBuilder<int, int>();
        for (int i = 0; i < 3000; i++) builder.Add(i, i);
        var map = builder.Build();

        var doubled = OrderedMapModule.MapValues((int v) => v * 2, map);
        var evens = OrderedMapModule.Filter((int k, int v) => k % 2 == 0, map);

        Assert.Equal(3000, doubled.Count);
        Assert.Equal(1500, evens.Count);
        Assert.True(doubled.TryGetValue(2999, out var d));
        Assert.Equal(5998, d);
        Assert.Equal(Enumerable.Range(0, 1500).Select(i => i * 2), evens.Select(kv => kv.Key));
    }

    [Fact]
    public void AddRangeTracksOrderToo()
    {
        var builder = new OrderedMapBuilder<int, string>();
        builder.Add(5, "five");
        builder.AddRange(new[] { new System.Collections.Generic.KeyValuePair<int, string>(1, "one") });

        var map = builder.Build();
        Assert.Equal(new[] { 1, 5 }, map.Select(kv => kv.Key));
    }
}
