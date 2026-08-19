using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions; 
using OrderedMap;

public class BTreeFuzzTests
{
    private readonly ITestOutputHelper _output;
    

    public BTreeFuzzTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Fuzz_Insert_And_Remove_consistency()
    {
        // CONFIGURATION
        const int iterations = 100_000; // High enough to trigger all splits/merges
        const int keyRange = 5000;      // Small enough to cause frequent collisions
        const bool showOps = false;
        int seed = 2135974; // Environment.TickCount;
        
        // ORACLES
        var reference = new SortedDictionary<int, int>();
        var subject = BaseOrderedMap<int, int>.CreateTransient();

        var random = new Random(seed);
        _output.WriteLine($"Starting Fuzz Test with Seed: {seed}");

        try
        {
            for (int i = 0; i < iterations; i++)
            {
                // 1. Pick an Action: 70% Insert/Update, 30% Remove
                bool isInsert = random.NextDouble() < 0.7;
                int key = random.Next(keyRange);
                int val = key * 100;

                if (isInsert)
                {
                    // ACTION: INSERT
                    if (showOps)Console.WriteLine($"insert: {key} : {val}");
                    if (key == 4436)
                    {
                        Console.WriteLine("BP");
                    }
                    
                    reference[key] = val;
                    subject.Set(key, val);
                }
                else
                {
                    // ACTION: REMOVE
                    if (reference.ContainsKey(key))
                    {
                        if (showOps)Console.WriteLine($"remove ${key}");
                        reference.Remove(key);
                        subject.Remove(key);
                    }
                    else
                    {
                        // Try removing non-existent key (should be safe)
                        subject.Remove(key);
                    }
                }

                // 2. VERIFY CONSISTENCY (Expensive but necessary)
                // We check consistency every 1000 ops or if the tree is small, 
                // to keep the test fast enough.
                //if (i % 1000 == 0 || reference.Count < 100)
                //{
                    AssertConsistency(reference, subject);
                //}
            }
            
            // Final check
            AssertConsistency(reference, subject);
        }
        catch (Exception)
        {
            _output.WriteLine($"FAILED at iteration with SEED: {seed}");
            throw; // Re-throw to fail the test
        }
    }
    
    [Fact]
    public void Fuzz_Range_Queries()
    {
        // Validates that your Range Enumerator matches LINQ on the reference
        const int iterations = 1000; 
        const int keyRange = 2000;
        int seed = Environment.TickCount;
        
        var reference = new SortedDictionary<int, int>();
        var subject = BaseOrderedMap<int, int>.CreateTransient();
        var random = new Random(seed);

        // Fill Data
        for(int i=0; i<keyRange; i++)
        {
            int k = random.Next(keyRange);
            reference[k] = k;
            subject.Set(k, k);
        }
        
        var persistent = subject.ToPersistent();

        for (int i = 0; i < iterations; i++)
        {
            int min = random.Next(keyRange);
            int max = min + random.Next(keyRange - min); // Ensure max >= min

            // 1. Reference Result (LINQ)
            // Note: SortedDictionary doesn't have a direct Range query, so we filter memory.
            var expected = reference
                .Where(kv => kv.Key >= min && kv.Key <= max)
                .Select(kv => kv.Key)
                .ToList();

            // 2. Subject Result
            var actual = persistent.Range(min, max)
                .Select(kv => kv.Key)
                .ToList();

            // 3. Compare
            if (!expected.SequenceEqual(actual))
            {
                _output.WriteLine($"Range Mismatch! Range: [{min}, {max}]");
                _output.WriteLine($"Expected: {string.Join(",", expected)}");
                _output.WriteLine($"Actual:   {string.Join(",", actual)}");
                Assert.Fail("Range query results differ.");
            }
        }
    }

    private void AssertConsistency(SortedDictionary<int, int> expected, TransientOrderedMap<int, int> actual)
    {
        // 1. Count
         if (expected.Count != actual.Count)
        {
            Console.WriteLine("BP");
             throw new Exception($"Count Mismatch! Expected {expected.Count}, Got {actual.Count}");
        }

        // 2. Full Scan Verification
        using var enumerator = actual.GetEnumerator();
        foreach (var kvp in expected)
        {
            if (!enumerator.MoveNext())
            {
                throw new Exception("Enumerator ended too early!");
            }
            
            if (enumerator.Current.Key != kvp.Key || enumerator.Current.Value != kvp.Value)
            {
                Console.WriteLine("BP");
                throw new Exception($"Content Mismatch! Expected [{kvp.Key}:{kvp.Value}], Got [{enumerator.Current.Key}:{enumerator.Current.Value}]");
            }
        }
        
        if (enumerator.MoveNext())
        {
            throw new Exception("Enumerator has extra items!");
        }
    }
}
