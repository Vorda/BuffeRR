using BenchmarkDotNet.Attributes;
using BuffeRR.Engine.Tables;

namespace BuffeRR.Benchmarks;

/// <summary>Point operations on an existing table. Each benchmark does one pass over all N keys (or N/10 for churn); divide by that for per-operation cost.</summary>
[MemoryDiagnoser]
public class TableAccessBenchmarks
{
    private Table<NodeRow> _table = new();
    private ulong[] _shuffledKeys = [];
    private NodeRow _row;

    [Params(1_000, 100_000, 1_000_000)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var entries = TableBenchmarkData.CreateEntries(Length);
        _table = new Table<NodeRow>(Length);
        _table.ReplaceAll(entries);
        _shuffledKeys = TableBenchmarkData.CreateShuffledKeys(Length);
        _row = entries[0].Row;
    }

    [Benchmark(Baseline = true)]
    public long TryGet_Hit()
    {
        long sum = 0;
        foreach (var key in _shuffledKeys)
        {
            if (_table.TryGet(key, out var row))
                sum += row.MeshId;
        }

        return sum;
    }

    [Benchmark]
    public int Set_Overwrite()
    {
        foreach (var key in _shuffledKeys)
            _table.Set(key, _row);
        return _table.Count;
    }

    /// <summary>Steady state: remove then re-add the same key, so Count is unchanged between invocations.</summary>
    [Benchmark]
    public int RemoveThenSet_Churn()
    {
        var churn = _shuffledKeys.Length / 10;
        for (var i = 0; i < churn; i++)
        {
            var key = _shuffledKeys[i];
            _table.Remove(key);
            _table.Set(key, _row);
        }

        return _table.Count;
    }
}
