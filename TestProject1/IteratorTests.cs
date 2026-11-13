using Xunit;
using OrderedMap;
using System.Linq;
using System.Collections.Generic;

public class EnumeratorTests
{
    // Use IntStrategy for simple numeric testing
    

    // Helper to create a populated OrderedMap quickly
    private OrderedMap<int, int> CreateMap(params int[] keys)
    {
        var map = BaseOrderedMap<int, int>.CreateTransient();
        foreach (var k in keys) 
        {
            map.Set(k, k * 10); // Value is key * 10 (e.g., 5 -> 50)
        }
        return map.ToPersistent();
    }

    [Fact]
    public void EmptyMap_EnumeratesNothing()
    {
        var map = OrderedMap<int, int>.Empty(null);
        var list = new List<int>();
        
        foreach(var kv in map) list.Add(kv.Key);
        
        Assert.Empty(list);
    }

    [Fact]
    public void FullScan_ReturnsSortedItems()
    {
        // Insert in random order to prove sorting works
        var map = CreateMap(5, 1, 3, 2, 4);
        
        var keys = map.AsEnumerable().Select(x => x.Key).ToList();

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, keys);
    }

    [Fact]
    public void Range_Inclusive_ExactMatches()
    {
        // Tree: 10, 20, 30, 40, 50
        var map = CreateMap(10, 20, 30, 40, 50);

        // Range 20..40 should give 20, 30, 40
        var result = map.Range(20, 40).Select(x => x.Key).ToList();

        Assert.Equal(new[] { 20, 30, 40 }, result);
    }

    [Fact]
    public void Range_Inclusive_WithGaps()
    {
        // Tree: 10, 20, 30, 40, 50
        var map = CreateMap(10, 20, 30, 40, 50);

        // Range 15..45:
        // Start: 15 -> Seeks to first key >= 15 (which is 20)
        // End: 45 -> Stops after 40 (next key 50 is > 45)
        var result = map.Range(15, 45).Select(x => x.Key).ToList();

        Assert.Equal(new[] { 20, 30, 40 }, result);
    }

    [Fact]
    public void Range_SingleItem_Exact()
    {
        var map = CreateMap(1, 2, 3);
        
        // Range 2..2 should just return 2
        var result = map.Range(2, 2).Select(x => x.Key).ToList();
        
        Assert.Single(result);
        Assert.Equal(2, result[0]);
    }

    [Fact]
    public void From_StartOpen_ToEnd()
    {
        // Tree: 10, 20, 30, 40, 50
        var map = CreateMap(10, 20, 30, 40, 50);

        // From 35 -> Should be 40, 50 (everything >= 35)
        var result = map.From(35).Select(x => x.Key).ToList();

        Assert.Equal(new[] { 40, 50 }, result);
    }

    [Fact]
    public void Until_StartTo_EndOpen()
    {
        // Tree: 10, 20, 30, 40, 50
        var map = CreateMap(10, 20, 30, 40, 50);

        // Until 25 -> Should be 10, 20 (everything <= 25)
        var result = map.Until(25).Select(x => x.Key).ToList();

        Assert.Equal(new[] { 10, 20 }, result);
    }

    [Fact]
    public void Range_OutOfBounds_ReturnsEmpty()
    {
        var map = CreateMap(10, 20, 30);
        
        // Range 40..50 (Past end)
        Assert.Empty(map.Range(40, 50));

        // Range 0..5 (Before start)
        Assert.Empty(map.Range(0, 5));
        
        // Range 15..16 (Gap between keys)
        Assert.Empty(map.Range(15, 16));
    }

    [Fact]
    public void Range_LargeDataset_CrossesLeafBoundaries()
    {
        // Force splits. Leaf Capacity is 32, so 100 items ensures ~3-4 leaves.
        var tMap = BaseOrderedMap<int, int>.CreateTransient();
        for(int i=0; i<100; i++) tMap.Set(i, i);
        var map = tMap.ToPersistent();

        // Range spanning multiple leaves (e.g. 20 to 80)
        var list = map.Range(20, 80).Select(x => x.Key).ToList();

        Assert.Equal(61, list.Count); // 20 through 80 inclusive is 61 items
        Assert.Equal(20, list.First());
        Assert.Equal(80, list.Last());
    }

    [Fact]
    public void Enumerator_Reset_Works()
    {
        // Verify that Reset() logic (re-seek/re-dive) works
        var map = CreateMap(1, 2, 3);
        using var enumerator = map.GetEnumerator();
        
        enumerator.MoveNext(); // 1
        enumerator.MoveNext(); // 2
        
        enumerator.Reset(); // Should go back to start
        
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current.Key);
    }
}