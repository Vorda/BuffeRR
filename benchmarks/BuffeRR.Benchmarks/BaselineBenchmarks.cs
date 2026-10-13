using BenchmarkDotNet.Attributes;

namespace BuffeRR.Benchmarks;

/// <summary>
/// M0 smoke benchmark: proves the benchmark pipeline runs and gives a sanity reference for this machine.
/// Replace with real table benchmarks in M1.
/// </summary>
[MemoryDiagnoser]
public class BaselineBenchmarks
{
    private int[] _data = [];

    [Params(1_000, 100_000)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup() => _data = Enumerable.Range(0, Length).ToArray();

    [Benchmark(Baseline = true)]
    public long ArrayForLoop()
    {
        long sum = 0;
        for (var i = 0; i < _data.Length; i++)
            sum += _data[i];
        return sum;
    }

    [Benchmark]
    public long SpanForeach()
    {
        long sum = 0;
        foreach (var value in _data.AsSpan())
            sum += value;
        return sum;
    }

    [Benchmark]
    public long LinqSum() => _data.Sum(static x => (long)x);
}
