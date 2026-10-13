namespace BuffeRR.Core.Tables;

/// <summary>
/// A typed table of unmanaged rows keyed by <see cref="ulong"/> (ADR-0003). Not thread-safe: all mutation belongs to the
/// single writer (ADR-0001). Semantics are specified in ADR-0008.
/// </summary>
public interface ITable<TRow> : ITableReader<TRow> where TRow : unmanaged
{
    /// <summary>Inserts or overwrites a row. Always counts as a change, even if the new value equals the old one. Returns true if the key was added.</summary>
    bool Set(ulong key, in TRow row);

    /// <summary>Mutates one row in place. Returns false (and changes nothing) if the key is absent.</summary>
    bool TryUpdate(ulong key, RowMutator<TRow> mutator);

    /// <inheritdoc cref="TryUpdate(ulong, RowMutator{TRow})"/>
    bool TryUpdate<TMutator>(ulong key, TMutator mutator) where TMutator : struct, IRowMutator<TRow>;

    /// <summary>Removes a row. Returns false (and changes nothing) if the key is absent. Row order is not preserved.</summary>
    bool Remove(ulong key);

    /// <summary>Mutates every row in place in one dense pass. The mutator is opaque, so every row is considered changed.</summary>
    void Transform(RowMutator<TRow> mutator);

    /// <inheritdoc cref="Transform(RowMutator{TRow})"/>
    void Transform<TMutator>(TMutator mutator) where TMutator : struct, IRowMutator<TRow>;

    /// <summary>
    /// Replaces the entire content as one operation (one revision). Throws <see cref="ArgumentException"/> for duplicate
    /// keys and leaves the table untouched in that case.
    /// </summary>
    void ReplaceAll(ReadOnlySpan<(ulong Key, TRow Row)> entries);

    /// <summary>Removes all rows. A no-op (no revision change) on an empty table.</summary>
    void Clear();
}
