using BuffeRR.Core.Tables;

namespace BuffeRR.Engine.Tables;

/// <summary>
/// Dense table of unmanaged rows keyed by <see cref="ulong"/>. Rows, keys and versions live in three parallel arrays;
/// a dictionary maps key to slot. Semantics are specified in ADR-0008.
/// <para>
/// Not thread-safe (single writer, ADR-0001). The span views stay valid only until the next mutating call.
/// </para>
/// </summary>
public sealed class Table<TRow> : ITable<TRow> where TRow : unmanaged
{
    private const int MinimumGrowth = 4;

    private TRow[] _rows;
    private ulong[] _keys;
    private ulong[] _versions;
    private Dictionary<ulong, int> _index;
    private int _count;
    private ulong _revision;
    private bool _inCallback;

    public Table(int initialCapacity = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        _rows = new TRow[initialCapacity];
        _keys = new ulong[initialCapacity];
        _versions = new ulong[initialCapacity];
        _index = new Dictionary<ulong, int>(initialCapacity);
    }

    public int Count => _count;

    public ulong Revision => _revision;

    /// <summary>Number of rows that fit without reallocating.</summary>
    public int Capacity => _rows.Length;

    public ReadOnlySpan<ulong> Keys => new(_keys, 0, _count);

    public ReadOnlySpan<TRow> Rows => new(_rows, 0, _count);

    public ReadOnlySpan<ulong> Versions => new(_versions, 0, _count);

    public bool ContainsKey(ulong key) => _index.ContainsKey(key);

    public bool TryGet(ulong key, out TRow row)
    {
        if (_index.TryGetValue(key, out var slot))
        {
            row = _rows[slot];
            return true;
        }

        row = default;
        return false;
    }

    public bool TryGetVersion(ulong key, out ulong version)
    {
        if (_index.TryGetValue(key, out var slot))
        {
            version = _versions[slot];
            return true;
        }

        version = 0;
        return false;
    }

    /// <summary>Grows storage so <paramref name="capacity"/> rows fit without reallocating. Never shrinks.</summary>
    public void EnsureCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ThrowIfInCallback();

        if (capacity > _rows.Length)
            Resize(capacity);
    }

    public bool Set(ulong key, in TRow row)
    {
        ThrowIfInCallback();
        var next = _revision + 1;

        if (_index.TryGetValue(key, out var slot))
        {
            _rows[slot] = row;
            _versions[slot] = next;
            _revision = next;
            return false;
        }

        if (_count == _rows.Length)
            Resize(NextCapacity());

        slot = _count;
        _rows[slot] = row;
        _keys[slot] = key;
        _versions[slot] = next;
        _index.Add(key, slot);
        _count++;
        _revision = next;
        return true;
    }

    public bool TryUpdate(ulong key, RowMutator<TRow> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        return TryUpdate(key, new DelegateMutator(mutator));
    }

    public bool TryUpdate<TMutator>(ulong key, TMutator mutator) where TMutator : struct, IRowMutator<TRow>
    {
        ThrowIfInCallback();

        if (!_index.TryGetValue(key, out var slot))
            return false;

        // Stamp BEFORE running the callback: if it throws, the version over-reports a change instead of
        // under-reporting one (ADR-0008, invariant 3).
        var next = _revision + 1;
        _versions[slot] = next;
        _revision = next;

        _inCallback = true;
        try
        {
            mutator.Mutate(ref _rows[slot]);
        }
        finally
        {
            _inCallback = false;
        }

        return true;
    }

    public bool Remove(ulong key)
    {
        ThrowIfInCallback();

        if (!_index.TryGetValue(key, out var slot))
            return false;

        var last = _count - 1;
        if (slot != last)
        {
            // Swap-remove keeps storage dense in O(1). The moved row keeps its key and its version.
            _rows[slot] = _rows[last];
            _keys[slot] = _keys[last];
            _versions[slot] = _versions[last];
            _index[_keys[slot]] = slot;
        }

        _index.Remove(key);
        _count--;
        _revision++;
        return true;
    }

    public void Transform(RowMutator<TRow> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        Transform(new DelegateMutator(mutator));
    }

    public void Transform<TMutator>(TMutator mutator) where TMutator : struct, IRowMutator<TRow>
    {
        ThrowIfInCallback();

        if (_count == 0)
            return;

        // One revision for the whole pass; stamped before mutating (see TryUpdate).
        var next = _revision + 1;
        _versions.AsSpan(0, _count).Fill(next);
        _revision = next;

        _inCallback = true;
        try
        {
            var rows = _rows.AsSpan(0, _count);
            for (var i = 0; i < rows.Length; i++)
                mutator.Mutate(ref rows[i]);
        }
        finally
        {
            _inCallback = false;
        }
    }

    public void ReplaceAll(ReadOnlySpan<(ulong Key, TRow Row)> entries)
    {
        ThrowIfInCallback();

        // Phase 1: validate and build the new index. Nothing observable is touched, so a duplicate key
        // leaves the table exactly as it was (strong exception guarantee).
        var newIndex = new Dictionary<ulong, int>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            if (!newIndex.TryAdd(entries[i].Key, i))
                throw new ArgumentException($"Duplicate key {entries[i].Key} at position {i}.", nameof(entries));
        }

        // Phase 2: allocate if needed (can throw OutOfMemory, still before any state changes), then commit.
        var rows = _rows;
        var keys = _keys;
        var versions = _versions;
        if (entries.Length > rows.Length)
        {
            rows = new TRow[entries.Length];
            keys = new ulong[entries.Length];
            versions = new ulong[entries.Length];
        }

        var next = _revision + 1;
        for (var i = 0; i < entries.Length; i++)
        {
            keys[i] = entries[i].Key;
            rows[i] = entries[i].Row;
        }

        versions.AsSpan(0, entries.Length).Fill(next);

        _rows = rows;
        _keys = keys;
        _versions = versions;
        _index = newIndex;
        _count = entries.Length;
        _revision = next;
    }

    public void Clear()
    {
        ThrowIfInCallback();

        if (_count == 0)
            return;

        _index.Clear();
        _count = 0;
        _revision++;
    }

    private int NextCapacity()
    {
        if (_count >= Array.MaxLength)
            throw new InvalidOperationException("The table has reached the maximum array length.");

        var doubled = Math.Max(MinimumGrowth, (long)_rows.Length * 2);
        return (int)Math.Min(doubled, Array.MaxLength);
    }

    // Allocate all three arrays first, then assign: an OutOfMemory part-way through cannot leave them with different lengths.
    private void Resize(int newCapacity)
    {
        var rows = new TRow[newCapacity];
        var keys = new ulong[newCapacity];
        var versions = new ulong[newCapacity];

        _rows.AsSpan(0, _count).CopyTo(rows);
        _keys.AsSpan(0, _count).CopyTo(keys);
        _versions.AsSpan(0, _count).CopyTo(versions);

        _rows = rows;
        _keys = keys;
        _versions = versions;
    }

    // A mutator that calls back into the table could reallocate storage while a ref into it is live.
    private void ThrowIfInCallback()
    {
        if (_inCallback)
            throw new InvalidOperationException("A table cannot be mutated from inside a RowMutator callback.");
    }

    private readonly struct DelegateMutator : IRowMutator<TRow>
    {
        private readonly RowMutator<TRow> _mutator;

        public DelegateMutator(RowMutator<TRow> mutator) => _mutator = mutator;

        public void Mutate(ref TRow row) => _mutator(ref row);
    }
}
