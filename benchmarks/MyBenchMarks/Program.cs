using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LanguageExt;
using OrderedMap;

namespace MapBenchmarks;

[MemoryDiagnoser]
public class IntMapBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N { get; set; }

    private int[] _allKeys;
    private int[] _retrieveKeys;
    private int[] _updateKeys;
    private int[] _removeKeys;
    private int[] _mixedKeys;

    private ImmutableSortedDictionary<int, int> _immSortedDict;
    private LanguageExt.Map<int, int> _extMap;
    private OrderedMap<int, int> _persistentOrderedMap;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(42);
        
        _allKeys = Enumerable.Range(0, N).ToArray();

        int subsetSize = Math.Max(1, N / 10);
        
        var shuffled = _allKeys.OrderBy(x => rnd.Next()).ToArray();
        _retrieveKeys = shuffled.Take(subsetSize).ToArray();
        _updateKeys = shuffled.Skip(subsetSize).Take(subsetSize).ToArray();
        _removeKeys = shuffled.Skip(subsetSize * 2).Take(subsetSize).ToArray();

        var existingHalf = shuffled.Skip(subsetSize * 3).Take(subsetSize / 2).ToArray();
        var newHalf = Enumerable.Range(N + 1, subsetSize - (subsetSize / 2)).ToArray();
        _mixedKeys = existingHalf.Concat(newHalf).OrderBy(x => rnd.Next()).ToArray();

        _immSortedDict = ImmutableSortedDictionary.CreateRange(_allKeys.Select(k => new KeyValuePair<int, int>(k, k)));
        
        _extMap = LanguageExt.Map.empty<int, int>();
        foreach (var k in _allKeys)
        {
            _extMap = _extMap.AddOrUpdate(k, k);
        }

        var transient = BaseOrderedMap<int, int>.CreateTransient();
        foreach (var k in _allKeys) transient.Set(k, k);
        _persistentOrderedMap = transient.ToPersistent();
    }

    // --- 1. BUILD ---

    [Benchmark]
    public ImmutableSortedDictionary<int, int> Build_ImmSortedDict()
    {
        var map = ImmutableSortedDictionary<int, int>.Empty;
        foreach (var k in _allKeys) map = map.Add(k, k);
        return map;
    }

    [Benchmark]
    public LanguageExt.Map<int, int> Build_ExtMap()
    {
        var map = LanguageExt.Map.empty<int, int>();
        foreach (var k in _allKeys) map = map.AddOrUpdate(k, k);
        return map;
    }

    [Benchmark]
    public OrderedMap<int, int> Build_PersistentMap()
    {
        var map = OrderedMap<int, int>.Create();
        foreach (var k in _allKeys) map = map.Set(k, k);
        return map;
    }

    [Benchmark]
    public OrderedMap<int, int> Build_TransientMap()
    {
        var map = BaseOrderedMap<int, int>.CreateTransient();
        foreach (var k in _allKeys) map.Set(k, k);
        return map.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedDictionary<int, int> Build_ImmSortedDict_Builder()
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<int, int>();
        foreach (var k in _allKeys) builder.Add(k, k);
        return builder.ToImmutable();
    }

    // --- 2. RETRIEVAL ---

    [Benchmark]
    public int Retrieve_ImmSortedDict()
    {
        int count = 0;
        foreach (var k in _retrieveKeys)
            if (_immSortedDict.TryGetValue(k, out _)) count++;
        return count;
    }

    [Benchmark]
    public int Retrieve_ExtMap()
    {
        int count = 0;
        foreach (var k in _retrieveKeys)
            if (_extMap.Find(k).IsSome) count++;
        return count;
    }

    [Benchmark]
    public int Retrieve_PersistentMap()
    {
        int count = 0;
        foreach (var k in _retrieveKeys)
            if (_persistentOrderedMap.TryGetValue(k, out _)) count++;
        return count;
    }

    // --- 3. UPDATING ---

    [Benchmark]
    public OrderedMap<int, int> Update_PersistentMap()
    {
        var map = _persistentOrderedMap;
        foreach (var k in _updateKeys) map = map.Set(k, 999);
        return map;
    }

    [Benchmark]
    public OrderedMap<int, int> Update_TransientMap()
    {
        var transient = _persistentOrderedMap.ToTransient();
        foreach (var k in _updateKeys) transient.Set(k, 999);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedDictionary<int, int> Update_ImmSortedDict()
    {
        var map = _immSortedDict;
        foreach (var k in _updateKeys) map = map.SetItem(k, 999);
        return map;
    }

    [Benchmark]
    public ImmutableSortedDictionary<int, int> Update_ImmSortedDict_Builder()
    {
        var builder = _immSortedDict.ToBuilder();
        foreach (var k in _updateKeys) builder[k] = 999;
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Map<int, int> Update_ExtMap()
    {
        var map = _extMap;
        foreach (var k in _updateKeys) map = map.SetItem(k, 999);
        return map;
    }

    // --- 4. UPDATE & SET (MIXED) ---

    [Benchmark]
    public OrderedMap<int, int> UpdateSet_PersistentMap()
    {
        var map = _persistentOrderedMap;
        foreach (var k in _mixedKeys) map = map.Set(k, 999);
        return map;
    }

    [Benchmark]
    public OrderedMap<int, int> UpdateSet_TransientMap()
    {
        var transient = _persistentOrderedMap.ToTransient();
        foreach (var k in _mixedKeys) transient.Set(k, 999);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedDictionary<int, int> UpdateSet_ImmSortedDict()
    {
        var map = _immSortedDict;
        foreach (var k in _mixedKeys) map = map.SetItem(k, 999);
        return map;
    }

    [Benchmark]
    public ImmutableSortedDictionary<int, int> UpdateSet_ImmSortedDict_Builder()
    {
        var builder = _immSortedDict.ToBuilder();
        foreach (var k in _mixedKeys) builder[k] = 999;
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Map<int, int> UpdateSet_ExtMap()
    {
        var map = _extMap;
        foreach (var k in _mixedKeys) map = map.AddOrUpdate(k, 999);
        return map;
    }

    // --- 5. ITERATION ---

    [Benchmark]
    public int Iterate_PersistentMap()
    {
        int sum = 0;
        foreach (var kvp in _persistentOrderedMap) sum += kvp.Value;
        return sum;
    }
    
    [Benchmark]
    public int Iterate_ImmSortedDict()
    {
        int sum = 0;
        foreach (var kvp in _immSortedDict) sum += kvp.Value;
        return sum;
    }

    [Benchmark]
    public int Iterate_ExtMap()
    {
        int sum = 0;
        foreach (var kvp in _extMap) sum += kvp.Value;
        return sum;
    }

    // --- 6. REMOVAL ---

    [Benchmark]
    public OrderedMap<int, int> Remove_PersistentMap()
    {
        var map = _persistentOrderedMap;
        foreach (var k in _removeKeys) map = map.Remove(k);
        return map;
    }
    
    [Benchmark]
    public OrderedMap<int, int> Remove_TransientMap()
    {
        var transient = _persistentOrderedMap.ToTransient();
        foreach (var k in _removeKeys) transient.Remove(k);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedDictionary<int, int> Remove_ImmSortedDict()
    {
        var map = _immSortedDict;
        foreach (var k in _removeKeys) map = map.Remove(k);
        return map;
    }

    [Benchmark]
    public ImmutableSortedDictionary<int, int> Remove_ImmSortedDict_Builder()
    {
        var builder = _immSortedDict.ToBuilder();
        foreach (var k in _removeKeys) builder.Remove(k);
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Map<int, int> Remove_ExtMap()
    {
        var map = _extMap;
        foreach (var k in _removeKeys) map = map.Remove(k);
        return map;
    }
    
    public static void Main(string[] args)
    {
        BenchmarkSwitcher
            .FromAssembly(typeof(IntMapBenchmarks).Assembly)
            .Run(args);
    }
}
