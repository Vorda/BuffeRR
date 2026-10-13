using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace SoftwareRR.BuffeRR;

public sealed class MemoryCacheRR<TValue> where TValue : unmanaged
{
    private TValue[] _values;
    private ulong[] _keys;
    private long[] _versions;          // NEW: per-entry version stamp
    private int _count;
    private readonly Dictionary<ulong, int> _index;

    public delegate void RefAction<T>(ref T item);

    public MemoryCacheRR(int initialCapacity = 1024)
    {
        if (initialCapacity <= 0) initialCapacity = 16;

        _values = new TValue[initialCapacity];
        _keys = new ulong[initialCapacity];
        _versions = new long[initialCapacity];
        _index = new(initialCapacity);
    }

    public int Count => _count;

    /// <summary>A view over the live entries. Do not hold across any Set/Apply/ReplaceAll/Remove call.</summary>
    public Span<TValue> AsSpan() => _values.AsSpan(0, _count);

    /// <summary>The live entries reinterpreted as raw bytes - zero copy. Useful for wire transfer or persistence.</summary>
    public Span<byte> AsBytes() => MemoryMarshal.AsBytes(AsSpan());

    /// <summary>
    /// Mutates every live entry in place in a single dense pass. Good for
    /// per-frame bulk updates (e.g. apply velocity to every node's position).
    /// </summary>
    public void Transform(RefAction<TValue> mutator)
    {
        Span<TValue> span = AsSpan();
        for (int i = 0; i < span.Length; i++)
            mutator(ref span[i]);
    }


    /// <summary>Inserts or overwrites a value for the given key.</summary>
    public void Set(ulong key, in TValue value)
    {
        if (_index.TryGetValue(key, out int slot))
        {
            _values[slot] = value;
            _versions[slot]++;
            return;
        }
        if (_count == _values.Length) Grow();
        _values[_count] = value;
        _keys[_count] = key;
        _versions[_count] = 1;
        _index[key] = _count;
        _count++;
    }

    public bool TryGet(ulong key, out TValue value)
    {
        if (_index.TryGetValue(key, out int slot)) {
            value = _values[slot]; 
            return true; 
        }

        value = default;
        return false;
    }

    public bool ContainsKey(ulong key) => _index.ContainsKey(key);

    // GetVersion matters because in CQRS you often want to know
    // "has this actually changed since I last read it" — useful for optimistic concurrency
    // on writes, or for a future notification / diff layer
    // (e.g. "which items changed today" without diffing full structs).

    public long GetVersion(ulong key) =>
        _index.TryGetValue(key, out int slot) ? _versions[slot] : -1;

    // Safe mutation - no dangling ref risk, because the ref never leaves this call frame
    public bool TryUpdate(ulong key, RefAction<TValue> mutator)
    {
        if (!_index.TryGetValue(key, out int slot)) return false;
        mutator(ref _values[slot]);
        _versions[slot]++;
        return true;
    }

    // THE event-sourcing primitive: fold an event into current state.
    // evolve receives (default if new key, current value if existing) -> next value
    public void Apply(ulong key, Func<TValue, TValue> evolve)
    {
        TValue current = TryGet(key, out var existing) ? existing : default;
        Set(key, evolve(current));
    }

    // sometimes we need: atomic-ish bulk replace, single pass, no per-item version noise
    //
    // ReplaceAll is deliberately a different code path from Apply-in-a-loop for the weekly rotation,
    // because doing 200 individual Set calls would churn _index with 200 dictionary ops and
    // leave version numbers in a weird "everything bumped once" state that doesn't mean anything semantically.
    // A full rotation isn't 200 events — conceptually it's one event - so the storage operation
    // should look like one operation too: clear, bulk-load, done.
    public void ReplaceAll(ReadOnlySpan<(ulong Key, TValue Value)> newMenu)
    {
        _count = 0;
        _index.Clear();
        if (newMenu.Length > _values.Length)
        {
            int size = _values.Length;
            while (size < newMenu.Length) size *= 2;
            _values = new TValue[size];
            _keys = new ulong[size];
            _versions = new long[size];
        }
        foreach (var (key, value) in newMenu)
        {
            _values[_count] = value;
            _keys[_count] = key;
            _versions[_count] = 1;
            _index[key] = _count;
            _count++;
        }
    }

    public bool Remove(ulong key)
    {
        if (!_index.TryGetValue(key, out int slot)) return false;
        int last = _count - 1;
        if (slot != last)
        {
            _values[slot] = _values[last];
            _keys[slot] = _keys[last];
            _versions[slot] = _versions[last];
            _index[_keys[slot]] = slot;
        }
        _count--;
        _index.Remove(key);
        return true;
    }

    private void Grow()
    {
        int newSize = _values.Length * 2;
        Array.Resize(ref _values, newSize);
        Array.Resize(ref _keys, newSize);
        Array.Resize(ref _versions, newSize);
    }

    public IEnumerable<TValue> Where(Expression<Func<TValue, bool>> predicate)
    {
        var compiled = predicate.Compile();
        for (int i = 0; i < _count; i++)
            if (compiled(_values[i]))
                yield return _values[i];
    }
}
