using System;
using System.Globalization;
using System.Linq;
using Xunit;
using OrderedMap;

namespace TestProject1;

/// A string-keyed map enumerates in ordinal order whatever the ambient culture
/// is. `Comparer<string>.Default` is culture-sensitive: under `sv-SE` it puts
/// "zebra" before "ärlig", under `en-US` after.
public class StringOrderTests
{
    private static readonly string[] Words = { "zebra", "ärlig", "apple", "Apple", "Ödla" };

    private static readonly string[] Ordinal = { "Apple", "apple", "zebra", "Ödla", "ärlig" };

    private static string[] KeysUnder(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var map = OrderedMapModule.Empty<string, int>();
            foreach (var w in Words) map = map.Set(w, w.Length);
            return map.Select(kv => kv.Key).ToArray();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void OrdinalUnderEveryCulture()
    {
        Assert.Equal(Ordinal, KeysUnder("en-US"));
        Assert.Equal(Ordinal, KeysUnder("sv-SE"));
        Assert.Equal(Ordinal, KeysUnder("tr-TR"));
    }

    [Fact]
    public void BuilderAgreesWithTheTree()
    {
        var builder = new OrderedMapBuilder<string, int>();
        foreach (var w in Words) builder.Add(w, w.Length);
        Assert.Equal(Ordinal, builder.Build().Select(kv => kv.Key).ToArray());
    }

    [Fact]
    public void TransientAgreesWithTheTree()
    {
        var t = TransientOrderedMapModule.Empty<string, int>();
        foreach (var w in Words) t.Set(w, w.Length);
        Assert.Equal(Ordinal, t.ToPersistent().Select(kv => kv.Key).ToArray());
    }

    /// A named comparer still wins — the ordinal default is only the default.
    [Fact]
    public void AnExplicitComparerIsHonoured()
    {
        var map = OrderedMapModule.Empty<string, int>(StringComparer.OrdinalIgnoreCase);
        map = map.Set("Apple", 1).Set("apple", 2);
        Assert.Equal(1, map.Count);
    }
}
