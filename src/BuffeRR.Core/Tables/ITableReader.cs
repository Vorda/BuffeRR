namespace BuffeRR.Core.Tables;

/// <summary>
/// Read side of a typed table. Semantics are specified in ADR-0008.
/// The span properties are views over live storage: they are valid only until the next mutating call on the table.
/// </summary>
public interface ITableReader<TRow> where TRow : unmanaged
{
    /// <summary>Number of live rows.</summary>
    int Count { get; }

    /// <summary>
    /// Table-wide, strictly increasing change counter. Incremented by exactly one for every effective mutating call.
    /// </summary>
    ulong Revision { get; }

    /// <summary>Keys of the live rows. Index <c>i</c> corresponds to <c>Rows[i]</c> and <c>Versions[i]</c>. Order is unspecified.</summary>
    ReadOnlySpan<ulong> Keys { get; }

    /// <summary>The live rows, parallel to <see cref="Keys"/>.</summary>
    ReadOnlySpan<TRow> Rows { get; }

    /// <summary>
    /// For each live row, the <see cref="Revision"/> at which it was last (possibly) changed. May over-report a change,
    /// never under-reports one.
    /// </summary>
    ReadOnlySpan<ulong> Versions { get; }

    bool ContainsKey(ulong key);

    bool TryGet(ulong key, out TRow row);

    bool TryGetVersion(ulong key, out ulong version);
}
