using System.Linq;
using Xunit;
using OrderedMap;

namespace PersistentMap.Tests
{
    public class BTreeExtendedOperationsTests
    {
        // Helper to quickly spin up a populated map
        private TransientOrderedMap<int, string> CreateMap(params int[] keys)
        {
            var map = BaseOrderedMap<int, string>.CreateTransient();
            foreach (var key in keys)
            {
                map.Set(key, $"val_{key}");
            }
            return map;
        }

        [Fact]
        public void MinMax_OnEmptyTree_ReturnsFalse()
        {
            var map = CreateMap();

            bool hasMin = map.TryGetMin(out int minKey, out string minVal);
            bool hasMax = map.TryGetMax(out int maxKey, out string maxVal);

            Assert.False(hasMin);
            Assert.False(hasMax);
            Assert.Equal(default, minKey);
            Assert.Equal(default, maxKey);
        }

        [Fact]
        public void MinMax_OnPopulatedTree_ReturnsCorrectExtremes()
        {
            var map = CreateMap(50, 10, 40, 20, 30); // Insert out of order

            bool hasMin = map.TryGetMin(out int minKey, out string minVal);
            bool hasMax = map.TryGetMax(out int maxKey, out string maxVal);

            Assert.True(hasMin);
            Assert.Equal(10, minKey);
            Assert.Equal("val_10", minVal);

            Assert.True(hasMax);
            Assert.Equal(50, maxKey);
            Assert.Equal("val_50", maxVal);
        }

        [Theory]
        [InlineData(20, true, 30)] // Exact match, get next
        [InlineData(25, true, 30)] // Missing key, gets first greater
        [InlineData(50, false, 0)] // Max element has no successor
        [InlineData(60, false, 0)] // Out of bounds high
        public void Successor_ReturnsCorrectNextKey(int searchKey, bool expectedSuccess, int expectedNextKey)
        {
            var map = CreateMap(10, 20, 30, 40, 50);

            bool success = map.TryGetSuccessor(searchKey, out int nextKey, out string nextVal);

            Assert.Equal(expectedSuccess, success);
            if (expectedSuccess)
            {
                Assert.Equal(expectedNextKey, nextKey);
                Assert.Equal($"val_{expectedNextKey}", nextVal);
            }
        }

        [Theory]
        [InlineData(40, true, 30)] // Exact match, get previous
        [InlineData(35, true, 30)] // Missing key, gets largest smaller
        [InlineData(10, false, 0)] // Min element has no predecessor
        [InlineData(5, false, 0)]  // Out of bounds low
        public void Predecessor_ReturnsCorrectPreviousKey(int searchKey, bool expectedSuccess, int expectedPrevKey)
        {
            var map = CreateMap(10, 20, 30, 40, 50);

            bool success = map.TryGetPredecessor(searchKey, out int prevKey, out string prevVal);

            Assert.Equal(expectedSuccess, success);
            if (expectedSuccess)
            {
                Assert.Equal(expectedPrevKey, prevKey);
                Assert.Equal($"val_{expectedPrevKey}", prevVal);
            }
        }

        [Fact]
        public void SuccessorPredecessor_CrossNodeBoundaries_WorksCorrectly()
        {
            // Insert 200 elements to guarantee leaf node splits (Capacity is 64)
            // and internal node creation.
            var keys = Enumerable.Range(1, 200).ToArray();
            var map = CreateMap(keys);

            // Test boundaries between multiple leaves
            for (int i = 1; i < 200; i++)
            {
                // Successor
                bool sFound = map.TryGetSuccessor(i, out int next, out _);
                Assert.True(sFound);
                Assert.Equal(i + 1, next);

                // Predecessor
                bool pFound = map.TryGetPredecessor(i + 1, out int prev, out _);
                Assert.True(pFound);
                Assert.Equal(i, prev);
            }
        }

        [Fact]
        public void SetOperations_Intersect_ReturnsCommonElements()
        {
            var mapA = CreateMap(1, 2, 3, 4, 5);
            var mapB = CreateMap(4, 5, 6, 7, 8);

            var intersect = mapA.Intersect(mapB).Select(kvp => kvp.Key).ToArray();

            Assert.Equal(new[] { 4, 5 }, intersect);
        }

        [Fact]
        public void SetOperations_Except_ReturnsElementsOnlyInFirstMap()
        {
            var mapA = CreateMap(1, 2, 3, 4, 5);
            var mapB = CreateMap(4, 5, 6, 7, 8);

            var aExceptB = mapA.Except(mapB).Select(kvp => kvp.Key).ToArray();
            var bExceptA = mapB.Except(mapA).Select(kvp => kvp.Key).ToArray();

            Assert.Equal(new[] { 1, 2, 3 }, aExceptB);
            Assert.Equal(new[] { 6, 7, 8 }, bExceptA);
        }

        [Fact]
        public void SetOperations_SymmetricExcept_ReturnsNonOverlappingElements()
        {
            var mapA = CreateMap(1, 2, 3, 4, 5);
            var mapB = CreateMap(4, 5, 6, 7, 8);

            var symmetric = mapA.SymmetricExcept(mapB).Select(kvp => kvp.Key).ToArray();

            // Should return elements exclusively in A or B, but not both.
            // Expected sorted naturally: 1, 2, 3, 6, 7, 8
            Assert.Equal(new[] { 1, 2, 3, 6, 7, 8 }, symmetric);
        }

        [Fact]
        public void SetOperations_WithEmptyMaps_HandleGracefully()
        {
            var populatedMap = CreateMap(1, 2, 3);
            var emptyMap = CreateMap();

            // Intersect with empty is empty
            Assert.Empty(populatedMap.Intersect(emptyMap));
            Assert.Empty(emptyMap.Intersect(populatedMap));

            // Populated Except empty is Populated
            Assert.Equal(new[] { 1, 2, 3 }, populatedMap.Except(emptyMap).Select(k => k.Key));
            
            // Empty Except Populated is empty
            Assert.Empty(emptyMap.Except(populatedMap));

            // Symmetric Except with empty is just the populated map
            Assert.Equal(new[] { 1, 2, 3 }, populatedMap.SymmetricExcept(emptyMap).Select(k => k.Key));
            Assert.Equal(new[] { 1, 2, 3 }, emptyMap.SymmetricExcept(populatedMap).Select(k => k.Key));
        }

        [Fact]
        public void SetOperations_CompleteOverlap_HandlesCorrectly()
        {
            var mapA = CreateMap(1, 2, 3);
            var mapB = CreateMap(1, 2, 3);

            Assert.Equal(new[] { 1, 2, 3 }, mapA.Intersect(mapB).Select(k => k.Key));
            Assert.Empty(mapA.Except(mapB));
            Assert.Empty(mapA.SymmetricExcept(mapB));
        }
    }
}
