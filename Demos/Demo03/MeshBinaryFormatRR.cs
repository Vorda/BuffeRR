using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SoftwareRR.Demos.AssetStreaming;

/// <summary>
/// A tiny placeholder binary mesh format standing in for the output of a real
/// FBX import step. Real FBX parsing needs a dedicated parser/SDK - that's a
/// separate concern from what this demo is about. The point here is the
/// caching/streaming architecture around already-imported mesh data, so we
/// produce data directly in this flat shape instead of parsing real FBX.
/// Layout: [int vertexCount][int indexCount][Vector3 vertices...][int indices...]
/// </summary>
public static class MeshBinaryFormatRR
{
    private const int HeaderBytes = sizeof(int) * 2;

    public static byte[] Encode(ReadOnlySpan<Vector3> vertices, ReadOnlySpan<int> indices)
    {
        int vertexBytes = vertices.Length * Unsafe.SizeOf<Vector3>();
        int indexBytes = indices.Length * sizeof(int);
        var buffer = new byte[HeaderBytes + vertexBytes + indexBytes];

        Span<int> header = MemoryMarshal.Cast<byte, int>(buffer.AsSpan(0, HeaderBytes));
        header[0] = vertices.Length;
        header[1] = indices.Length;

        MemoryMarshal.AsBytes(vertices).CopyTo(buffer.AsSpan(HeaderBytes, vertexBytes));
        MemoryMarshal.AsBytes(indices).CopyTo(buffer.AsSpan(HeaderBytes + vertexBytes, indexBytes));

        return buffer;
    }

    /// <summary>Zero-copy reinterpretation of a stored blob back into vertex/index spans.</summary>
    public static MeshView Parse(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<int> header = MemoryMarshal.Cast<byte, int>(data[..HeaderBytes]);
        int vertexCount = header[0];
        int indexCount = header[1];

        int vertexBytes = vertexCount * Unsafe.SizeOf<Vector3>();
        var vertices = MemoryMarshal.Cast<byte, Vector3>(data.Slice(HeaderBytes, vertexBytes));
        var indices = MemoryMarshal.Cast<byte, int>(data.Slice(HeaderBytes + vertexBytes, indexCount * sizeof(int)));

        return new MeshView(vertices, indices);
    }
}

/// <summary>A zero-copy view over a parsed mesh blob. Ref struct - do not store, just read and discard.</summary>
public readonly ref struct MeshView
{
    public readonly ReadOnlySpan<Vector3> Vertices;
    public readonly ReadOnlySpan<int> Indices;

    public MeshView(ReadOnlySpan<Vector3> vertices, ReadOnlySpan<int> indices)
    {
        Vertices = vertices;
        Indices = indices;
    }
}