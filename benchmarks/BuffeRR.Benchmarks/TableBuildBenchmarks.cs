using BenchmarkDotNet.Attributes;
using BuffeRR.Engine.Tables;

namespace BuffeRR.Benchmarks;

/// <summary>Cost of building a table of N rows. Each invocation allocates a fresh table, so allocation is part of the number on purpose.</summary>
[MemoryDiagnoser]
public class TableBuildBenchmarks
{
    private (ulong Key, NodeRow Row)[] _entries = [];

    [Params(1_000, 100_000, 1_000_000)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup() => _entries = TableBenchmarkData.CreateEntries(Length);

    [Benchmark(Baseline = true)]
    public int Set_GrowingFromEmpty()
    {
        var table = new Table<NodeRow>();
        foreach (var (key, row) in _entries)
            table.Set(key, row);
        return table.Count;
    }

    [Benchmark]
    public int Set_Presized()
    {
        var table = new Table<NodeRow>(Length);
        foreach (var (key, row) in _entries)
            table.Set(key, row);
        return table.Count;
    }

    [Benchmark]
    public int ReplaceAll_Bulk()
    {
        var table = new Table<NodeRow>();
        table.ReplaceAll(_entries);
        return table.Count;
    }
}
