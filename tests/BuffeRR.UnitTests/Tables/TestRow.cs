using BuffeRR.Core.Tables;

namespace BuffeRR.UnitTests.Tables;

/// <summary>Small unmanaged row used by table tests.</summary>
public record struct TestRow(long A, int B);

/// <summary>Struct mutator: exercises the allocation-free generic overloads.</summary>
public readonly struct AddToA : IRowMutator<TestRow>
{
    private readonly long _amount;

    public AddToA(long amount) => _amount = amount;

    public void Mutate(ref TestRow row) => row.A += _amount;
}
