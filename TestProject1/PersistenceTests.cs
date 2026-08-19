namespace TestProject1;
using OrderedMap;
public class PersistenceTests
{
    

    [Fact]
    public void PersistentMap_IsTrulyImmutable()
    {
        var v0 = BaseOrderedMap<string, int>.Create();
        var v1 = v0.Set("A", 1);
        var v2 = v1.Set("B", 2);

        // v0 should be empty
        Assert.False(v0.ContainsKey("A"));
        
        // v1 should have A but not B
        Assert.True(v1.ContainsKey("A"));
        Assert.False(v1.ContainsKey("B"));

        // v2 should have both
        Assert.True(v2.ContainsKey("A"));
        Assert.True(v2.ContainsKey("B"));
    }

    [Fact]
    public void TransientToPersistent_CreatesSnapshot()
    {
        var tMap = BaseOrderedMap<string, int>.CreateTransient();
        tMap.Set("A", 1);

        // Create Snapshot
        var pMap = tMap.ToPersistent();

        // Mutate Transient Map further
        tMap.Set("B", 2);
        tMap.Remove("A");

        // Assert Transient State
        Assert.False(tMap.ContainsKey("A"));
        Assert.True(tMap.ContainsKey("B"));

        // Assert Persistent Snapshot (Should be isolated)
        Assert.True(pMap.ContainsKey("A")); // A should still be here
        Assert.False(pMap.ContainsKey("B")); // B should not exist
    }

    [Fact]
    public void PersistentToTransient_DoesNotCorruptSource()
    {
        var pMap = BaseOrderedMap<string, int>.Create();
        pMap = pMap.Set("Fixed", 1);

        var tMap = pMap.ToTransient();
        tMap.Set("Fixed", 999); // Modify shared key

        // pMap should remain 1
        Assert.True(pMap.TryGetValue("Fixed", out int val));
        Assert.Equal(1, val);
    }
}