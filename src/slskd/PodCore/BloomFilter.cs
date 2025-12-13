// <copyright file="BloomFilter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections;

/// <summary>
/// A probabilistic Bloom filter for efficient membership testing.
/// Provides constant-time lookups with configurable false positive rates.
/// </summary>
public class BloomFilter
{
    private readonly BitArray _bitArray;
    private readonly int _hashFunctionCount;
    private readonly int _size;
    private readonly Func<string, int>[] _hashFunctions;
    private long _itemCount;

    /// <summary>
    /// Initializes a new instance of the BloomFilter class.
    /// </summary>
    /// <param name="expectedItems">Expected number of items to be added.</param>
    /// <param name="falsePositiveRate">Desired false positive rate (between 0 and 1).</param>
    public BloomFilter(int expectedItems, double falsePositiveRate = 0.01)
    {
        if (expectedItems <= 0)
            throw new ArgumentException("Expected items must be positive", nameof(expectedItems));
        if (falsePositiveRate <= 0 || falsePositiveRate >= 1)
            throw new ArgumentException("False positive rate must be between 0 and 1", nameof(falsePositiveRate));

        // Calculate optimal size and hash functions
        _size = CalculateOptimalSize(expectedItems, falsePositiveRate);
        _hashFunctionCount = CalculateOptimalHashFunctions(expectedItems, _size);

        _bitArray = new BitArray(_size);
        _hashFunctions = CreateHashFunctions(_hashFunctionCount);

        ExpectedItems = expectedItems;
        FalsePositiveRate = falsePositiveRate;
    }

    /// <summary>
    /// Gets the expected number of items.
    /// </summary>
    public int ExpectedItems { get; }

    /// <summary>
    /// Gets the target false positive rate.
    /// </summary>
    public double FalsePositiveRate { get; }

    /// <summary>
    /// Gets the current number of items added.
    /// </summary>
    public long ItemCount => _itemCount;

    /// <summary>
    /// Gets the fill ratio of the filter.
    /// </summary>
    public double FillRatio
    {
        get
        {
            int setBits = 0;
            for (int i = 0; i < _bitArray.Length; i++)
            {
                if (_bitArray.Get(i)) setBits++;
            }
            return (double)setBits / _size;
        }
    }

    /// <summary>
    /// Adds an item to the Bloom filter.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>True if the item was added (not previously present), false if it might have been present.</returns>
    public bool Add(string item)
    {
        if (string.IsNullOrEmpty(item))
            throw new ArgumentException("Item cannot be null or empty", nameof(item));

        var wasPresent = Contains(item);

        foreach (var hash in _hashFunctions)
        {
            var index = hash(item) % _size;
            _bitArray.Set(index, true);
        }

        if (!wasPresent)
        {
            _itemCount++;
        }

        return !wasPresent;
    }

    /// <summary>
    /// Tests whether an item is in the Bloom filter.
    /// </summary>
    /// <param name="item">The item to test.</param>
    /// <returns>True if the item is definitely NOT in the filter, false if it might be in the filter.</returns>
    public bool Contains(string item)
    {
        if (string.IsNullOrEmpty(item))
            return false;

        foreach (var hash in _hashFunctions)
        {
            var index = hash(item) % _size;
            if (!_bitArray.Get(index))
            {
                return false; // Definitely not present
            }
        }

        return true; // Might be present (or false positive)
    }

    /// <summary>
    /// Clears all items from the Bloom filter.
    /// </summary>
    public void Clear()
    {
        _bitArray.SetAll(false);
        _itemCount = 0;
    }

    /// <summary>
    /// Estimates the current false positive rate.
    /// </summary>
    /// <returns>The estimated false positive rate.</returns>
    public double EstimateFalsePositiveRate()
    {
        if (_itemCount == 0)
            return 0.0;

        var fillRatio = FillRatio;
        return Math.Pow(fillRatio, _hashFunctionCount);
    }

    /// <summary>
    /// Calculates the optimal Bloom filter size.
    /// </summary>
    private static int CalculateOptimalSize(int expectedItems, double falsePositiveRate)
    {
        // m = -n * ln(p) / (ln(2)^2)
        var ln2Squared = Math.Log(2) * Math.Log(2);
        var size = -(double)expectedItems * Math.Log(falsePositiveRate) / ln2Squared;
        return (int)Math.Ceiling(size);
    }

    /// <summary>
    /// Calculates the optimal number of hash functions.
    /// </summary>
    private static int CalculateOptimalHashFunctions(int expectedItems, int size)
    {
        // k = m/n * ln(2)
        var optimal = (double)size / expectedItems * Math.Log(2);
        return Math.Max(1, (int)Math.Round(optimal));
    }

    /// <summary>
    /// Creates hash functions using double hashing technique.
    /// </summary>
    private Func<string, int>[] CreateHashFunctions(int count)
    {
        var functions = new Func<string, int>[count];

        for (int i = 0; i < count; i++)
        {
            var seed1 = i * 2 + 1;
            var seed2 = i * 2 + 2;

            functions[i] = (string item) =>
            {
                // Use double hashing: h1 + i*h2
                var hash1 = GetStableHash(item, seed1);
                var hash2 = GetStableHash(item, seed2);

                // Ensure positive hash value
                return Math.Abs(hash1 + i * hash2);
            };
        }

        return functions;
    }

    /// <summary>
    /// Generates a stable hash for a string using a seeded algorithm.
    /// </summary>
    private static int GetStableHash(string input, int seed)
    {
        // Simple but stable hash function using FNV-1a variant
        const uint prime = 16777619;
        uint hash = 2166136261 ^ (uint)seed;

        foreach (char c in input)
        {
            hash = (hash ^ c) * prime;
        }

        return (int)hash;
    }
}

/// <summary>
/// A time-windowed Bloom filter that automatically expires old entries.
/// </summary>
public class TimeWindowedBloomFilter
{
    private readonly BloomFilter _filter;
    private readonly TimeSpan _windowSize;
    private DateTimeOffset _currentWindowStart;
    private DateTimeOffset _lastCleanup;

    /// <summary>
    /// Initializes a new instance of the TimeWindowedBloomFilter class.
    /// </summary>
    /// <param name="expectedItemsPerWindow">Expected items per time window.</param>
    /// <param name="windowSize">Size of each time window.</param>
    /// <param name="falsePositiveRate">Desired false positive rate.</param>
    public TimeWindowedBloomFilter(int expectedItemsPerWindow, TimeSpan windowSize, double falsePositiveRate = 0.01)
    {
        _filter = new BloomFilter(expectedItemsPerWindow, falsePositiveRate);
        _windowSize = windowSize;
        _currentWindowStart = DateTimeOffset.UtcNow;
        _lastCleanup = DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Adds an item to the filter with timestamp-based expiration.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>True if the item was newly added.</returns>
    public bool Add(string item)
    {
        CleanupIfNeeded();
        return _filter.Add(item);
    }

    /// <summary>
    /// Tests whether an item might be in the current filter window.
    /// </summary>
    /// <param name="item">The item to test.</param>
    /// <returns>True if the item might be present.</returns>
    public bool Contains(string item)
    {
        CleanupIfNeeded();
        return _filter.Contains(item);
    }

    /// <summary>
    /// Gets statistics about the filter.
    /// </summary>
    public (long ItemCount, double FillRatio, double EstimatedFalsePositiveRate) GetStats()
    {
        return (_filter.ItemCount, _filter.FillRatio, _filter.EstimateFalsePositiveRate());
    }

    /// <summary>
    /// Forces cleanup of expired entries.
    /// </summary>
    public void ForceCleanup()
    {
        _filter.Clear();
        _currentWindowStart = DateTimeOffset.UtcNow;
        _lastCleanup = DateTimeOffset.UtcNow;
    }

    private void CleanupIfNeeded()
    {
        var now = DateTimeOffset.UtcNow;

        if (now - _currentWindowStart >= _windowSize)
        {
            // Time to rotate the window
            _filter.Clear();
            _currentWindowStart = now;
            _lastCleanup = now;
        }
    }
}