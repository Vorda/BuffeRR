namespace BuffeRR.Core.Tables;

/// <summary>Delegate form of an in-place row mutation. Convenient, but captures allocate and the call cannot be inlined.</summary>
public delegate void RowMutator<TRow>(ref TRow row) where TRow : unmanaged;

/// <summary>
/// Struct-friendly form of an in-place row mutation. Pass an implementing <c>struct</c> to the generic
/// <c>Transform</c>/<c>TryUpdate</c> overloads: the JIT specializes the call per struct type, so the mutation can be
/// inlined and nothing is allocated.
/// </summary>
public interface IRowMutator<TRow> where TRow : unmanaged
{
    void Mutate(ref TRow row);
}
