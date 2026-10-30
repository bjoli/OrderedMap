using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using OrderedMap;

// MergeAll and MergeWithAll give what merging two at a time gives, for inputs
// of every shape: few and many (past the k-way limit), small and big, the
// biggest first, in the middle and last, disjoint and overlapping keys,
// empty ones, and inputs ordered by different comparers.
public class ManyInputsTests
{
    private static OrderedMap<int, int> Map(Random rng, int size, int range, int tag, IComparer<int>? order = null)
    {
        var t = BaseOrderedMap<int, int>.CreateTransient(order);
        for (var i = 0; i < size; i++)
        {
            var k = rng.Next(range);
            t.Set(k, k * 1000 + tag);
        }
        return t.ToPersistent();
    }

    private static void Same(OrderedMap<int, int> want, OrderedMap<int, int> got, string what)
    {
        Assert.True(want.Count == got.Count, $"{what}: count {got.Count}, want {want.Count}");
        Assert.True(want.SequenceEqual(got), $"{what}: entries differ");
        foreach (var kv in want)
            Assert.True(got.TryGetValue(kv.Key, out var v) && v == kv.Value, $"{what}: key {kv.Key}");
    }

    // A resolver whose result depends on the order and the grouping of its arguments.
    private static int Combine(int key, int a, int b) => unchecked(a * 31 + b + key);

    private static List<OrderedMap<int, int>> Inputs(Random rng, int round)
    {
        var count = rng.Next(4) == 0 ? rng.Next(2, 4) : rng.Next(2, 60);
        var range = rng.Next(2) == 0 ? 1_000_000 : rng.Next(50, 5000);
        var big = rng.Next(count);
        var hasBig = rng.Next(2) == 0;
        var maps = new List<OrderedMap<int, int>>();
        for (var i = 0; i < count; i++)
        {
            var size = hasBig && i == big ? rng.Next(5000, 20000)
                : rng.Next(5) == 0 ? 0
                : rng.Next(3) == 0 ? rng.Next(1, 10) : rng.Next(10, 3000);
            maps.Add(Map(rng, size, range, round * 100 + i));
        }
        return maps;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ManyGiveWhatTwoAtATimeGive(int seed)
    {
        var rng = new Random(seed);
        for (var round = 0; round < 60; round++)
        {
            var maps = Inputs(rng, round);
            var rest = maps.Skip(2).ToArray();
            var what = $"seed {seed} round {round}: {maps.Count} maps of {string.Join(",", maps.Select(m => m.Count))}";

            Same(maps.Aggregate(OrderedMapModule.Merge), OrderedMapModule.MergeAll(maps[0], maps[1], rest), what);
            Same(maps.Aggregate((a, b) => OrderedMapModule.MergeWith(Combine, a, b)),
                 OrderedMapModule.MergeWithAll(Combine, maps[0], maps[1], rest), what + ", with");
        }
    }

    [Fact]
    public void InputsInAnotherOrderAreMergedTwoAtATime()
    {
        var rng = new Random(7);
        var reverse = Comparer<int>.Create((a, b) => b.CompareTo(a));
        var maps = new[] { Map(rng, 300, 1000, 1), Map(rng, 300, 1000, 2, reverse), Map(rng, 300, 1000, 3) };
        Same(maps.Aggregate(OrderedMapModule.Merge), OrderedMapModule.MergeAll(maps[0], maps[1], maps[2..]), "merge");
        Same(maps.Aggregate((a, b) => OrderedMapModule.MergeWith(Combine, a, b)),
             OrderedMapModule.MergeWithAll(Combine, maps[0], maps[1], maps[2..]), "merge with");
    }

    [Fact]
    public void EmptyInputs()
    {
        var empty = OrderedMapModule.Empty<int, int>();
        var one = Map(new Random(1), 100, 1000, 1);
        Assert.Equal(0, OrderedMapModule.MergeAll(empty, empty, new[] { empty }).Count);
        Assert.Same(one, OrderedMapModule.MergeAll(empty, one, new[] { empty }));
        Assert.Same(one, OrderedMapModule.MergeWithAll(Combine, one, empty, Array.Empty<OrderedMap<int, int>>()));
    }
}
