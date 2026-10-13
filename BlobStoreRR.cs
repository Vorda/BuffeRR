using System;
using System.Collections.Generic;
using System.Buffers;

namespace SoftwareRR.BuffeRR;

/// <summary>
/// Pool-backed store for large, variable-length, version-stamped blobs
/// (imported meshes, textures, any asset that doesn't fit the fixed-size
/// slot model MemoryCache<TValue> needs). Each blob owns its own rented
/// buffer rather than living inside one shared arena, which buys two things:
///
/// 1. Exposes ReadOnlyMemory<byte> not Span<byte> so results can
///    cross await boundaries - the shape BlobStreamWriter/Reader need to
///    stream a blob out over a real async transport.
/// 2. Replacing a blob's version returns the old buffer to the pool
///    immediately instead of leaving it as permanent dead space.
/// </summary>
public sealed class BlobStoreRR : IDisposable
{
    private sealed class BlobEntryRR
    {
        public required IMemoryOwner<byte> Owner { get; init; }
        public required int Length { get; init; }
        public required uint Version { get; init; }
    }

    private readonly Dictionary<int, BlobEntryRR> _index = new();
    private readonly MemoryPool<byte> _pool = MemoryPool<byte>.Shared;
    private bool _disposed;

    public int Count => _index.Count;

    /// <summary>Sum of the currently-live blob lengths. Unlike an arena design, overwritten versions are excluded - their buffers were already returned to the pool.</summary>
    public long TotalBytesResident
    {
        get
        {
            long total = 0;
            foreach (var entry in _index.Values) total += entry.Length;
            return total;
        }
    }

    /// <summary>Stores a new blob, or a new version of an existing one. Overwritten versions release their buffer immediately.</summary>
    public void Store(int id, ReadOnlySpan<byte> data, uint version)
    {
        ThrowIfDisposed();

        var owner = _pool.Rent(data.Length);
        data.CopyTo(owner.Memory.Span);

        if (_index.TryGetValue(id, out var old))
            old.Owner.Dispose(); // return the previous version's buffer to the pool - real reclaim, not dead space

        _index[id] = new BlobEntryRR { Owner = owner, Length = data.Length, Version = version };
    }

    /// <summary>
    /// Async-safe view over the blob bytes. Call `.Span` on the result only for
    /// synchronous work that doesn't cross an await - for an actual async send,
    /// pass the ReadOnlyMemory<byte> itself straight into the write call.
    /// </summary>
    public ReadOnlyMemory<byte> Get(int id)
    {
        ThrowIfDisposed();
        return _index.TryGetValue(id, out var entry)
            ? entry.Owner.Memory[..entry.Length]
            : ReadOnlyMemory<byte>.Empty;
    }

    public bool TryGetVersion(int id, out uint version)
    {
        if (_index.TryGetValue(id, out var entry))
        {
            version = entry.Version;
            return true;
        }

        version = 0;
        return false;
    }

    public int GetByteLength(int id) =>
        _index.TryGetValue(id, out var entry) ? entry.Length : 0;

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BlobStoreRR));
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var entry in _index.Values)
            entry.Owner.Dispose();

        _index.Clear();
        _disposed = true;
    }
}