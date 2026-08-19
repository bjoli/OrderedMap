using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BenchmarkDotNet.Attributes;
using LanguageExt;
using OrderedMap;

namespace MapBenchmarks;

[MemoryDiagnoser]
public class StringMapBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N { get; set; }

    [Params(8, 50)]
    public int StringLength { get; set; }

    private string[] _allKeys;
    private string[] _retrieveKeys;
    private string[] _updateKeys;
    private string[] _removeKeys;
    private string[] _mixedKeys;

    private ImmutableSortedDictionary<string, int> _immSortedDict;
    private LanguageExt.Map<string, int> _extMap;
    
    private OrderedMap<string, int> _persistentOrderedMap;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(42);
        
        _allKeys = Enumerable.Range(0, N).Select(_ => GenerateRandomString(StringLength, rnd)).Distinct().ToArray();
        
        while (_allKeys.Length < N)
        {
            _allKeys = _allKeys.Concat(new[] { GenerateRandomString(StringLength, rnd) }).Distinct().ToArray();
        }

        int subsetSize = Math.Max(1, N / 10);
        
        var shuffled = _allKeys.OrderBy(x => rnd.Next()).ToArray();
        _retrieveKeys = shuffled.Take(subsetSize).ToArray();
        _updateKeys = shuffled.Skip(subsetSize).Take(subsetSize).ToArray();
        _removeKeys = shuffled.Skip(subsetSize * 2).Take(subsetSize).ToArray();

        var existingHalf = shuffled.Skip(subsetSize * 3).Take(subsetSize / 2).ToArray();
        var newHalf = Enumerable.Range(0, subsetSize - (subsetSize / 2)).Select(_ => GenerateRandomString(StringLength, rnd)).ToArray();
        _mixedKeys = existingHalf.Concat(newHalf).OrderBy(x => rnd.Next()).ToArray();

        _immSortedDict = ImmutableSortedDictionary.CreateRange(_allKeys.Select((k, i) => new KeyValuePair<string, int>(k, i)));
        
        _extMap = LanguageExt.Map.empty<string, int>();
        for (int i = 0; i < _allKeys.Length; i++)
        {
            _extMap = _extMap.AddOrUpdate(_allKeys[i], i);
        }

        var transient = BaseOrderedMap<string, int>.CreateTransient();
        for (int i = 0; i < _allKeys.Length; i++)
        {
            transient.Set(_allKeys[i], i);
        }
        _persistentOrderedMap = transient.ToPersistent();
    }

    private static string GenerateRandomString(int length, Random rnd)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length).Select(s => s[rnd.Next(s.Length)]).ToArray());
    }

    // --- 1. BUILD ---
    
    [Benchmark]
    public OrderedMap<string, int> Build_PersistentMap()
    {
        var map = OrderedMap<string, int>.Create();
        for (int i = 0; i < _allKeys.Length; i++) map = map.Set(_allKeys[i], i);
        return map;
    }

    [Benchmark]
    public OrderedMap<string, int> Build_TransientMap()
    {
        var map = BaseOrderedMap<string, int>.CreateTransient();
        for (int i = 0; i < _allKeys.Length; i++) map.Set(_allKeys[i], i);
        return map.ToPersistent();
    }

    [Benchmark]
    public ImmutableSortedDictionary<string, int> Build_ImmSortedDict()
    {
        var map = ImmutableSortedDictionary<string, int>.Empty;
        for (int i = 0; i < _allKeys.Length; i++) map = map.Add(_allKeys[i], i);
        return map;
    }

    [Benchmark]
    public ImmutableSortedDictionary<string, int> Build_ImmSortedDict_Builder()
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, int>();
        for (int i = 0; i < _allKeys.Length; i++) builder.Add(_allKeys[i], i);
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Map<string, int> Build_ExtMap()
    {
        var map = LanguageExt.Map.empty<string, int>();
        for (int i = 0; i < _allKeys.Length; i++) map = map.AddOrUpdate(_allKeys[i], i);
        return map;
    }

    // --- 2. RETRIEVAL ---

    [Benchmark]
    public int Retrieve_PersistentMap()
    {
        int count = 0;
        foreach (var k in _retrieveKeys)
            if (_persistentOrderedMap.TryGetValue(k, out _)) count++;
        return count;
    }
    
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

    // --- 3. UPDATING ---

    [Benchmark]
    public OrderedMap<string, int> Update_PersistentMap()
    {
        var map = _persistentOrderedMap;
        foreach (var k in _updateKeys) map = map.Set(k, 999);
        return map;
    }

    [Benchmark]
    public OrderedMap<string, int> Update_TransientMap()
    {
        var transient = _persistentOrderedMap.ToTransient();
        foreach (var k in _updateKeys) transient.Set(k, 999);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedDictionary<string, int> Update_ImmSortedDict()
    {
        var map = _immSortedDict;
        foreach (var k in _updateKeys) map = map.SetItem(k, 999);
        return map;
    }

    [Benchmark]
    public ImmutableSortedDictionary<string, int> Update_ImmSortedDict_Builder()
    {
        var builder = _immSortedDict.ToBuilder();
        foreach (var k in _updateKeys) builder[k] = 999;
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Map<string, int> Update_ExtMap()
    {
        var map = _extMap;
        foreach (var k in _updateKeys) map = map.SetItem(k, 999);
        return map;
    }

    // --- 4. UPDATE & SET (MIXED) ---

    [Benchmark]
    public OrderedMap<string, int> UpdateSet_PersistentMap()
    {
        var map = _persistentOrderedMap;
        foreach (var k in _mixedKeys) map = map.Set(k, 999);
        return map;
    }

    [Benchmark]
    public OrderedMap<string, int> UpdateSet_TransientMap()
    {
        var transient = _persistentOrderedMap.ToTransient();
        foreach (var k in _mixedKeys) transient.Set(k, 999);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedDictionary<string, int> UpdateSet_ImmSortedDict()
    {
        var map = _immSortedDict;
        foreach (var k in _mixedKeys) map = map.SetItem(k, 999);
        return map;
    }

    [Benchmark]
    public ImmutableSortedDictionary<string, int> UpdateSet_ImmSortedDict_Builder()
    {
        var builder = _immSortedDict.ToBuilder();
        foreach (var k in _mixedKeys) builder[k] = 999;
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Map<string, int> UpdateSet_ExtMap()
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
    public OrderedMap<string, int> Remove_PersistentMap()
    {
        var map = _persistentOrderedMap;
        foreach (var k in _removeKeys) map = map.Remove(k);
        return map;
    }

    [Benchmark]
    public OrderedMap<string, int> Remove_TransientMap()
    {
        var transient = _persistentOrderedMap.ToTransient();
        foreach (var k in _removeKeys) transient.Remove(k);
        return transient.ToPersistent();
    }
    
    [Benchmark]
    public ImmutableSortedDictionary<string, int> Remove_ImmSortedDict()
    {
        var map = _immSortedDict;
        foreach (var k in _removeKeys) map = map.Remove(k);
        return map;
    }

    [Benchmark]
    public ImmutableSortedDictionary<string, int> Remove_ImmSortedDict_Builder()
    {
        var builder = _immSortedDict.ToBuilder();
        foreach (var k in _removeKeys) builder.Remove(k);
        return builder.ToImmutable();
    }

    [Benchmark]
    public LanguageExt.Map<string, int> Remove_ExtMap()
    {
        var map = _extMap;
        foreach (var k in _removeKeys) map = map.Remove(k);
        return map;
    }
}
