using SoftwareRR.BuffeRR;
using SoftwareRR.Demos.AssetStreaming;
using System.IO.Pipelines;
using System.Numerics;


const int MeshAssetCount = 12;
const int NodeCount = 5_000;
const float WorldExtent = 500f;
const float InterestRadius = 60f;

var assets = new BlobStoreRR();

Console.WriteLine("=== Importing mesh assets (simulated offline FBX import step) ===");
for (int meshId = 1; meshId <= MeshAssetCount; meshId++)
{
    int vertexCount = 5000 + meshId * 40; // distribute the size a bit
    byte[] encoded = MeshImporterRR.ImportFakeAsset(meshId, vertexCount, version: 1);
    assets.Store(meshId, encoded, version: 1);
}

Console.WriteLine($"Imported {assets.Count} assets, {assets.TotalBytesResident:N0} bytes total in the arena");

Console.WriteLine("\n=== Scattering nodes across the world (this is the part that moves every frame) ===");
var rng = new Random(50);
var world = new MemoryCacheRR<StreamNodeRR>(NodeCount);
var nodeEntries = new (ulong, StreamNodeRR)[NodeCount];
for (int i = 0; i < NodeCount; i++)
{
    ulong id = (ulong)(i + 1);
    nodeEntries[i] = (id, new StreamNodeRR
    {
        Id = id,
        MeshId = rng.Next(1, MeshAssetCount + 1),
        Position = new Vector3(
            (float)(rng.NextDouble() * WorldExtent * 2 - WorldExtent),
            0f,
            (float)(rng.NextDouble() * WorldExtent * 2 - WorldExtent))
    });
}
world.ReplaceAll(nodeEntries);
Console.WriteLine($"World holds {world.Count:N0} nodes referencing {MeshAssetCount} shared mesh assets");

var clientKnownVersions = new Dictionary<int, uint>();
long totalSent = 0;

Console.WriteLine("\n=== Client streaming simulation ===");

totalSent += SimulateClientFrame(
    "Frame 1 (client enters area at origin)",
    new Vector3(0, 0, 0), InterestRadius, world, assets, clientKnownVersions);

totalSent += SimulateClientFrame(
    "Frame 2 (client shifts slightly, same neighborhood)",
    new Vector3(5, 0, -3), InterestRadius, world, assets, clientKnownVersions);

Console.WriteLine("\n=== Server re-imports one mesh at a new version (e.g. LOD/texture refresh on mesh 3) ===");
byte[] updated = MeshImporterRR.ImportFakeAsset(meshId: 3, vertexCount: 50 + 3 * 40 + 20, version: 2);
assets.Store(3, updated, version: 2);
Console.WriteLine($"Mesh 3 is now version 2. Arena is {assets.TotalBytesResident:N0} bytes.");

totalSent += SimulateClientFrame("Frame 3 (same neighborhood, only mesh 3 changed)", new Vector3(5, 0, -3), InterestRadius, world, assets, clientKnownVersions);

Console.WriteLine($"\nTotal bytes sent to this one client across all 3 frames: {totalSent:N0}");
Console.WriteLine($"Total bytes resident in the server's asset arena:          {assets.TotalBytesResident:N0}");
Console.WriteLine("Node position/transform data moved every frame and never touched the asset arena at all -");
Console.WriteLine("MemoryCache<StreamNode> and AssetBlobStore are two stores with two different lifecycles.");

static HashSet<int> FindNearbyMeshIds(MemoryCacheRR<StreamNodeRR> cache, Vector3 clientPos, float radius)
{
    float radiusSq = radius * radius;
    var meshIds = new HashSet<int>();

    foreach (ref readonly var node in cache.AsSpan())
    {
        if (Vector3.DistanceSquared(node.Position, clientPos) <= radiusSq)
            meshIds.Add(node.MeshId);
    }

    return meshIds;
}

static long SimulateClientFrame(
    string label,
    Vector3 clientPos,
    float radius,
    MemoryCacheRR<StreamNodeRR> world,
    BlobStoreRR assets,
    Dictionary<int, uint> clientKnownVersions)
{
    var nearbyMeshIds = FindNearbyMeshIds(world, clientPos, radius);
    long bytesSent = 0;
    int assetsSent = 0;

    foreach (int meshId in nearbyMeshIds)
    {
        if (!assets.TryGetVersion(meshId, out uint serverVersion)) continue;

        bool clientHasCurrent = clientKnownVersions.TryGetValue(meshId, out uint clientVersion)
                                 && clientVersion == serverVersion;

        if (clientHasCurrent) continue; // already downloaded and confirmed at this version - skip

        int len = assets.GetByteLength(meshId);
        bytesSent += len;
        assetsSent++;
        clientKnownVersions[meshId] = serverVersion;
    }

    Console.WriteLine($"{label}: {nearbyMeshIds.Count} meshes in range, {assetsSent} transferred, {bytesSent:N0} bytes sent");
    return bytesSent;
}


Console.WriteLine("\n=== Pipelines round-trip (no network yet - proving the frame format itself works) ===");
await DemoPipeRoundTripAsync(assets);

static async Task DemoPipeRoundTripAsync(BlobStoreRR assets)
{
    // Pipe is an in-memory producer/consumer buffer - the standard way to
    // exercise PipeWriter/PipeReader logic without a real socket. Later,
    // swap this for PipeWriter.Create(networkStream)/PipeReader.Create(networkStream)
    // for TCP, or a manual adapter that copies received UDP datagrams into
    // pipe.Writer for UDP - the framing code above never has to change.
    var pipe = new Pipe();

    assets.TryGetVersion(3, out uint version);
    ReadOnlyMemory<byte> payload = assets.Get(3);

    var readTask = Task.Run(async () =>
    {
        int framesRead = 0;
        await BlobStreamReaderRR.ReadFramesAsync(pipe.Reader, (header, sequence) =>
        {
            framesRead++;
            Console.WriteLine($"  Reconstructed frame: assetId={header.AssetId}, version={header.Version}, " +
                               $"{header.PayloadLength} bytes, single contiguous segment={sequence.IsSingleSegment}");
        });
        return framesRead;
    });

    await BlobStreamWriterRR.WriteFrameAsync(pipe.Writer, assetId: 3, version, payload);
    await pipe.Writer.CompleteAsync();

    int framesRead = await readTask;
    Console.WriteLine($"  Round-trip complete: {framesRead} frame(s) reconstructed with zero manual byte-offset math on the caller's side.");
}
