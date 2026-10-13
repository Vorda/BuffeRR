```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method       | Length | Mean        | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------- |------- |------------:|---------:|---------:|------:|--------:|----------:|------------:|
| **ArrayForLoop** | **1000**   |    **445.9 ns** |  **5.52 ns** |  **5.16 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| SpanForeach  | 1000   |    222.9 ns |  0.33 ns |  0.31 ns |  0.50 |    0.01 |         - |          NA |
| LinqSum      | 1000   |    335.6 ns |  1.50 ns |  1.40 ns |  0.75 |    0.01 |         - |          NA |
|              |        |             |          |          |       |         |           |             |
| **ArrayForLoop** | **100000** | **43,489.0 ns** | **75.15 ns** | **66.62 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| SpanForeach  | 100000 | 21,795.2 ns | 56.05 ns | 49.69 ns |  0.50 |    0.00 |         - |          NA |
| LinqSum      | 100000 | 32,710.0 ns | 98.55 ns | 87.36 ns |  0.75 |    0.00 |         - |          NA |
