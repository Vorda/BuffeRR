using System;
using System.Numerics;

namespace SoftwareRR.Demos.AssetStreaming;

/// <summary>
/// Stands in for an offline "FBX -> flat binary" import step. In a real
/// pipeline this runs once per asset version, outside the hot path, using a
/// proper FBX parser/SDK; here we generate plausible-looking vertex/index
/// data so the demo can stay focused on storage and streaming, not parsing.
/// </summary>
public static class MeshImporterRR
{
    public static byte[] ImportFakeAsset(int meshId, int vertexCount, uint version)
    {
        var rng = new Random(meshId * 397 + (int)version);

        var vertices = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = new Vector3(
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1));
        }

        // Simple triangle-fan indexing - just enough to have plausible index data.
        int triangleCount = Math.Max(0, vertexCount - 2);
        var indices = new int[triangleCount * 3];
        for (int t = 0; t < triangleCount; t++)
        {
            indices[t * 3 + 0] = 0;
            indices[t * 3 + 1] = t + 1;
            indices[t * 3 + 2] = t + 2;
        }

        return MeshBinaryFormatRR.Encode(vertices, indices);
    }
}
