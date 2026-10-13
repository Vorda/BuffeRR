using Microsoft.VisualBasic;
using SoftwareRR.BuffeRR;
using SoftwareRR.Demos.SceneGraph;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

// Vector3/Quaternion from System.Numerics are already hardware-accelerated (SSE/AVX/NEON under the hood), so this struct is a genuinely good fit for the .NET 10 JIT auto-vectorization
// i used earlier — simple loops over a Span<Node> doing arithmetic on .Position are exactly the shape that gets vectorized for free.
// Things worth demoing here specifically:
// - Bulk write pattern — ReplaceAll with 50k–100k generated nodes, timed.
// - Scan/query cost — Where(n => n.Position.X > threshold) (frustum-cull-style) via an expression-compiled Where,
//   benchmarked against a hand-written foreach over AsSpan().
//   This is the number that tells me the real cost of the LINQ/expression abstraction vs. raw span iteration — worth
//   knowing precisely rather than assuming.
// - In-place bulk mutation — "translate every node by a frame's velocity" is the classic game-loop op.

const int NodeCount = 100_000;

Console.WriteLine($"=== Generating {NodeCount:N0} nodes ===");
var generated = GenerateScene(NodeCount);

var cache = new MemoryCacheRR<NodeRR>(NodeCount);

// --- Bulk load: "load a whole scene at once" - one pass, no per-node dictionary churn.
var sw = Stopwatch.StartNew();
cache.ReplaceAll(generated);
sw.Stop();
Console.WriteLine($"ReplaceAll bulk load: {sw.Elapsed.TotalMilliseconds:0.00} ms for {cache.Count:N0} nodes");

// --- Per-frame bulk mutation: move every node by a velocity vector in one
// dense pass. This is the shape the .NET 10 JIT is able to auto-vectorize
// when the body is simple arithmetic over a Span<T>.
var velocity = new Vector3(0f, -0.01f, 0f);

sw.Restart();
cache.Transform((ref NodeRR n) => n.Position += velocity);
sw.Stop();
Console.WriteLine($"Transform (translate all nodes): {sw.Elapsed.TotalMilliseconds:0.00} ms");

// --- Query cost: expression-compiled Where(...) vs a hand-written loop over
// AsSpan(). Same predicate, two execution paths, so you can see exactly what
// the LINQ-style abstraction costs you.
//
// the Where(expr) vs raw foreach timing comparison will be noisy on a first JIT-cold run — run it a few times
// or wrap in a warm-up loop for a trustworthy number.
const float threshold = 0f;

sw.Restart();
int countViaWhere = 0;
foreach (var n in cache.Where(n => n.Position.Y > threshold))
    countViaWhere++;
sw.Stop();
Console.WriteLine($"Where(expr) scan:   {sw.Elapsed.TotalMilliseconds:0.000} ms, matched {countViaWhere:N0}");

sw.Restart();
int countViaSpan = 0;
foreach (ref readonly var n in cache.AsSpan())
{
    if (n.Position.Y > threshold) countViaSpan++;
}
sw.Stop();
Console.WriteLine($"Raw span foreach:    {sw.Elapsed.TotalMilliseconds:0.000} ms, matched {countViaSpan:N0}");

// --- Zero-copy byte view: the whole node table reinterpreted as raw bytes.
// This is the hook point for handing the buffer straight to a UDP send call
// with no serialization step at all.
Span<byte> raw = cache.AsBytes();
Console.WriteLine($"\nAsBytes(): {raw.Length:N0} bytes for {cache.Count:N0} nodes " +
                   $"({raw.Length / (double)cache.Count:0.0} bytes/node, sizeof(Node) = {Marshal.SizeOf<NodeRR>()})");

// Round-trip check: reinterpret those same bytes back as Node and confirm
// nothing was lost - this is the same trick MemoryMarshal.Cast does inside AsSpan().
var roundTripped = MemoryMarshal.Cast<byte, NodeRR>(raw);
Console.WriteLine($"Round-trip check, node[0].Id matches: {roundTripped[0].Id == generated[0].Item2.Id}");

Console.WriteLine("Press any key...");
Console.ReadKey();

static (ulong, NodeRR)[] GenerateScene(int count)
{
    var rng = new Random(42);
    var result = new (ulong, NodeRR)[count];

    for (int i = 0; i < count; i++)
    {
        ulong id = (ulong)(i + 1);
        var node = new NodeRR
        {
            Id = id,
            ParentId = i == 0 ? 0UL : id - 1, // simple chain for demo purposes
            MeshId = rng.Next(0, 64),
            Position = new Vector3(
                (float)(rng.NextDouble() * 200 - 100),
                (float)(rng.NextDouble() * 200 - 100),
                (float)(rng.NextDouble() * 200 - 100)),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        };

        result[i] = (id, node);
    }

    return result;
}

// proper memory footprint measure
static long MeasureManaged(Func<object> build)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    long before = GC.GetTotalMemory(true);
    var obj = build();
    long after = GC.GetTotalMemory(true);
    GC.KeepAlive(obj); // prevent the object from being collected before we measure
    return after - before;
}