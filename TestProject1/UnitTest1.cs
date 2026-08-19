namespace TestProject1;

using Xunit;
using OrderedMap;

public class BasicTests
{
    

    [Fact]
    public void Transient_InsertAndGet_Works()
    {
        var map = BaseOrderedMap<string, int>.CreateTransient();

        map.Set("Apple", 1);
        map.Set("Banana", 2);
        map.Set("Cherry", 3);

        Assert.True(map.TryGetValue("Apple", out int v1));
        Assert.Equal(1, v1);
        
        Assert.True(map.TryGetValue("Banana", out int v2));
        Assert.Equal(2, v2);

        Assert.False(map.TryGetValue("Date", out _));
    }

    [Fact]
    public void Transient_Update_Works()
    {
        var map = BaseOrderedMap<string, int>.CreateTransient();
        map.Set("Key", 100);
        map.Set("Key", 200); // Overwrite

        map.TryGetValue("Key", out int val);
        Assert.Equal(200, val);
    }

    [Fact]
    public void Transient_Remove_Works()
    {
        var map = BaseOrderedMap<string, int>.CreateTransient();
        map.Set("A", 1);
        map.Set("B", 2);
        map.Set("C", 3);

        map.Remove("B");

        Assert.True(map.ContainsKey("A"));
        Assert.False(map.ContainsKey("B"));
        Assert.True(map.ContainsKey("C"));
    }

    [Fact]
    public void Transient_PrefixCollision_HandlesCollision()
    {
        // UnicodeStrategy only packs the first 4 chars.
        // "Test1" and "Test2" have the same prefix "Test".
        var map = BaseOrderedMap<string, int>.CreateTransient();

        map.Set("Test1", 1);
        map.Set("Test2", 2);

        Assert.True(map.TryGetValue("Test1", out var v1));
        Assert.Equal(1, v1);

        Assert.True(map.TryGetValue("Test2", out var v2));
        Assert.Equal(2, v2);
    }
}