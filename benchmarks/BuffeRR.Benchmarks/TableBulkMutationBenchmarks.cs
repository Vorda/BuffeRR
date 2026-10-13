using System.Numerics;
using BenchmarkDotNet.Attributes;
using BuffeRR.Core.Tables;
using BuffeRR.Engine.Tables;

namespace BuffeRR.Benchmarks;

/// <summary>
/// The per-frame operations: translate every node, and scan for a predicate. The raw-array loop is the floor: it shows
/// what the table abstraction costs relative to a hand-written loop over the same data.
/// </summary>
[MemoryDiagnoser]
public class TableBulkMutationBenchmarks
{
    private static readonly Vector3 Velocity = new(0f, -0.01f, 0f);

    private Table<NodeRow> _table = new();
    private NodeRow[] _raw = [];
    private RowMutator<NodeRow> _delegateMutator = static (ref NodeRow row) => row.Position += Velocity;

    [Params(1_000, 100_000, 1_000_000)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var entries = TableBenchmarkData.CreateEntries(Length);
        _table = new Table<NodeRow>(Length);
        _table.ReplaceAll(entries);
        _raw = entries.Select(e => e.Row).ToArray();
        _delegateMutator = static (ref NodeRow row) => row.Position += Velocity;
    }

    [Benchmark(Baseline = true)]
    public void RawArrayLoop()
    {
        var span = _raw.AsSpan();
        for (var i = 0; i < span.Length; i++)
            span[i].Position += Velocity;
    }

    [Benchmark]
    public void Transform_StructMutator() => _table.Transform(new Translate(Velocity));

    [Benchmark]
    public void Transform_Delegate() => _table.Transform(_delegateMutator);

    [Benchmark]
    public int Scan_RowsSpan()
    {
        var count = 0;
        foreach (ref readonly var row in _table.Rows)
        {
            if (row.Position.Y > 0f)
                count++;
        }

        return count;
    }

    private readonly struct Translate : IRowMutator<NodeRow>
    {
        private readonly Vector3 _velocity;

        public Translate(Vector3 velocity) => _velocity = velocity;

        public void Mutate(ref NodeRow row) => row.Position += _velocity;
    }
}
