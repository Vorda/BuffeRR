namespace BuffeRR.UnitTests.Tables;

/// <summary>
/// Deliberately naive reference implementation of the ADR-0008 semantics (a Dictionary plus a counter), used as the
/// oracle in differential tests. It must stay simple enough to be obviously correct; never "optimize" it.
/// </summary>
public sealed class TableModel
{
    private readonly Dictionary<ulong, (TestRow Row, ulong Version)> _entries = [];

    public ulong Revision { get; private set; }

    public int Count => _entries.Count;

    public IReadOnlyDictionary<ulong, (TestRow Row, ulong Version)> Entries => _entries;

    public bool Set(ulong key, TestRow row)
    {
        var isNew = !_entries.ContainsKey(key);
        Revision++;
        _entries[key] = (row, Revision);
        return isNew;
    }

    public bool TryUpdate(ulong key, long addToA)
    {
        if (!_entries.TryGetValue(key, out var entry))
            return false;

        Revision++;
        _entries[key] = (entry.Row with { A = entry.Row.A + addToA }, Revision);
        return true;
    }

    public bool Remove(ulong key)
    {
        if (!_entries.Remove(key))
            return false;

        Revision++;
        return true;
    }

    public void Transform(long addToA)
    {
        if (_entries.Count == 0)
            return;

        Revision++;
        foreach (var key in _entries.Keys.ToList())
            _entries[key] = (_entries[key].Row with { A = _entries[key].Row.A + addToA }, Revision);
    }

    public void ReplaceAll(IReadOnlyList<(ulong Key, TestRow Row)> entries)
    {
        Revision++;
        _entries.Clear();
        foreach (var (key, row) in entries)
            _entries.Add(key, (row, Revision));
    }

    public void Clear()
    {
        if (_entries.Count == 0)
            return;

        Revision++;
        _entries.Clear();
    }
}
