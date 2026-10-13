using BenchmarkDotNet.Running;

// Run in Release only:  dotnet run -c Release --project benchmarks/BuffeRR.Benchmarks -- --filter "*"
// Results worth keeping go to docs/perf/ (see README, "Engineering standards").
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
