```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9711/25H2/2025Update/HudsonValley2)
AMD Ryzen 5 5600X 3.70GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                  | Length  | Mean           | Error        | StdDev       | Median         | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------ |-------- |---------------:|-------------:|-------------:|---------------:|------:|--------:|----------:|------------:|
| **RawArrayLoop**            | **1000**    |       **462.3 ns** |      **8.72 ns** |      **8.15 ns** |       **462.7 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| Transform_StructMutator | 1000    |       576.2 ns |      4.13 ns |      3.86 ns |       576.6 ns |  1.25 |    0.02 |         - |          NA |
| Transform_Delegate      | 1000    |     1,608.2 ns |     10.05 ns |      8.39 ns |     1,605.4 ns |  3.48 |    0.06 |         - |          NA |
| Scan_RowsSpan           | 1000    |       752.4 ns |     10.22 ns |      9.06 ns |       753.7 ns |  1.63 |    0.03 |         - |          NA |
|                         |         |                |              |              |                |       |         |           |             |
| **RawArrayLoop**            | **100000**  |    **49,862.8 ns** |    **461.21 ns** |    **385.13 ns** |    **49,847.5 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Transform_StructMutator | 100000  |    70,145.0 ns |    942.61 ns |    881.72 ns |    70,051.9 ns |  1.41 |    0.02 |         - |          NA |
| Transform_Delegate      | 100000  |   147,388.9 ns |  2,212.75 ns |  1,961.54 ns |   147,236.3 ns |  2.96 |    0.04 |         - |          NA |
| Scan_RowsSpan           | 100000  |   372,901.5 ns |  2,451.34 ns |  2,292.99 ns |   373,393.4 ns |  7.48 |    0.07 |         - |          NA |
|                         |         |                |              |              |                |       |         |           |             |
| **RawArrayLoop**            | **1000000** |   **633,991.9 ns** | **12,797.93 ns** | **37,332.17 ns** |   **618,654.2 ns** |  **1.00** |    **0.08** |         **-** |          **NA** |
| Transform_StructMutator | 1000000 | 1,165,385.5 ns | 22,104.53 ns | 26,313.87 ns | 1,170,251.2 ns |  1.84 |    0.11 |         - |          NA |
| Transform_Delegate      | 1000000 | 1,889,688.0 ns | 25,844.52 ns | 24,174.98 ns | 1,881,778.5 ns |  2.99 |    0.17 |         - |          NA |
| Scan_RowsSpan           | 1000000 | 3,851,720.9 ns |  4,671.51 ns |  4,369.73 ns | 3,852,625.0 ns |  6.09 |    0.34 |         - |          NA |
