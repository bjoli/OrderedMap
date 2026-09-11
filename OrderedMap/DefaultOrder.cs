using System;
using System.Collections.Generic;

namespace OrderedMap;

/// <summary>
/// The comparer a map gets when the caller names none.
/// </summary>
///
/// <remarks>
/// <para>
/// <c>Comparer&lt;string&gt;.Default</c> is <c>string.CompareTo</c>, which
/// compares in the ambient culture: under <c>sv-SE</c> "ä" sorts after "z",
/// under <c>en-US</c> it sorts beside "a". A tree keyed by strings would then
/// enumerate in a different order on a different machine, and a key written by
/// one process would be looked up against another process's ordering.
/// </para>
/// <para>
/// Strings therefore default to ordinal. Every other type keeps
/// <c>Comparer&lt;T&gt;.Default</c>, which reaches the type's own
/// <c>IComparable&lt;T&gt;</c>. Pass a comparer explicitly to get culture back.
/// </para>
/// </remarks>
internal static class DefaultOrder
{
    public static IComparer<T> For<T>() =>
        typeof(T) == typeof(string)
            ? (IComparer<T>)(object)StringComparer.Ordinal
            : Comparer<T>.Default;
}
