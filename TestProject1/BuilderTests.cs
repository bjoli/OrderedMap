using System;
using System.Linq;
using Xunit;
using OrderedMap;

namespace TestProject1;

public class BuilderTests
{
    [Fact]
    public void TestBuilderCreatesCorrectTree()
    {
        var builder = new OrderedMapBuilder<int, string>();
        
        for (int i = 0; i < 1000; i++)
        {
            builder.Add(i, i.ToString());
        }

        var map = builder.Build();
        Assert.Equal(1000, map.Count);
        
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(map.TryGetValue(i, out var value));
            Assert.Equal(i.ToString(), value);
        }
    }

    [Fact]
    public void TestBuilderHandlesDuplicatesWithStableSort()
    {
        var builder = new OrderedMapBuilder<int, string>();
        
        // Add duplicates out of order
        builder.Add(1, "First-1");
        builder.Add(3, "First-3");
        builder.Add(2, "First-2");
        builder.Add(1, "Second-1"); // Should overwrite First-1
        builder.Add(2, "Second-2"); // Should overwrite First-2

        var map = builder.Build();
        
        Assert.Equal(3, map.Count);
        
        Assert.True(map.TryGetValue(1, out var val1));
        Assert.Equal("Second-1", val1);
        
        Assert.True(map.TryGetValue(2, out var val2));
        Assert.Equal("Second-2", val2);
        
        Assert.True(map.TryGetValue(3, out var val3));
        Assert.Equal("First-3", val3);
    }
}
