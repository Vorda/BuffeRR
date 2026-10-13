using System.Numerics;

namespace SoftwareRR.Demos.AssetStreaming;

/// <summary>
/// A world object placed in the frame-mutated MemoryCache StreamNode.
/// Deliberately tiny and fixed-size - it only points at a MeshId, it never
/// carries mesh data itself. The mesh bytes live in AssetBlobStore, a
/// completely separate store with a completely different lifecycle.
/// </summary>
public struct StreamNodeRR
{
    public ulong Id;
    public int MeshId;
    public Vector3 Position;
}
