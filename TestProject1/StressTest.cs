namespace TestProject1;

using OrderedMap;

public class StressTests
{
    

    [Fact]
    public void LargeInsert_SplitsCorrectly()
    {
        var map = BaseOrderedMap<string, int>.CreateTransient();
        int count = 10_000;

        // 1. Insert 10k items
        for (int i = 0; i < count; i++)
        {
            // Pad with 0s to ensure consistent length sorting for simple debugging
            map.Set($"Key_{i:D6}", i);
        }
        

        // 2. Read back all items
        for (int i = 0; i < count; i++)
        {
            bool found = map.TryGetValue($"Key_{i:D6}", out int val);
            Assert.True(found, $"Failed to find Key_{i:D6}");
            Assert.Equal(i, val);
        }

        // 3. Verify Non-existent
        Assert.False(map.ContainsKey("Key_999999"));
    }

    [Fact]
    public void ReverseInsert_HandlesPrependSplits()
    {
        // Inserting in reverse order triggers the "Left/Right 90/10" split heuristic specific to prepends
        var map = BaseOrderedMap<string, int>.CreateTransient();
        int count = 5000;

        for (int i = count; i > 0; i--)
        {
            map.Set(i.ToString("D6"), i);
        }

        for (int i = 1; i <= count; i++)
        {
            Assert.True(map.ContainsKey(i.ToString("D6")));
        }
    }

    [Fact]
    public void Random_InsertDelete_Churn()
    {
        // Fuzzing test to catch edge cases in Rebalance/Merge
        var map = BaseOrderedMap<string, int>.CreateTransient();
        var rng = new Random(12345);
        var reference = new Dictionary<string, int>();

        for (int i = 0; i < 5000; i++)
        {
            string key = rng.Next(0, 1000).ToString(); // High collision chance
            int op = rng.Next(0, 3); // 0=Set, 1=Remove, 2=Check

            if (op == 0)
            {
                map.Set(key, i);
                reference[key] = i;
            }
            else if (op == 1)
            {
                map.Remove(key);
                reference.Remove(key);
            }
            else
            {
                bool mapHas = map.TryGetValue(key, out int v1);
                bool refHas = reference.TryGetValue(key, out int v2);
                
                Assert.Equal(refHas, mapHas);
                if (mapHas) Assert.Equal(v2, v1);
            }
        }
        Console.WriteLine("bp");
    }
}