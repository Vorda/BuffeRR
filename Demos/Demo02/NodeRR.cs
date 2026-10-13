using System.Numerics;

namespace SoftwareRR.Demos.SceneGraph;

/// <summary>
/// A scene graph node. Every field is unmanaged (Vector3/Quaternion are just
/// blittable float bundles), so thousands of these live as one dense,
/// span-castable, byte-castable block - no per-node heap allocation at all.
/// </summary>
public struct NodeRR
{
    public ulong Id;
    public ulong ParentId;
    public int MeshId;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}
