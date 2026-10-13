using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SoftwareRR.BuffeRR;

/// <summary>
/// Writes a blob out onto any PipeWriter as one length-prefixed frame:
/// [int AssetId][uint Version][int PayloadLength][payload bytes...]
///
/// This is transport-agnostic on purpose - a PipeWriter can be backed by an
/// in-memory Pipe (for testing, as in Demo.AssetStreaming), a TCP
/// NetworkStream (via PipeWriter.Create), or a manual adapter you write
/// yourself over a UDP socket. The framing logic never needs to know which.
/// </summary>
public static class BlobStreamWriterRR
{
    public const int HeaderSize = sizeof(int) + sizeof(uint) + sizeof(int); // AssetId + Version + PayloadLength

    public static async ValueTask WriteFrameAsync(
        PipeWriter writer,
        int assetId,
        uint version,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        // Header goes out as its own contiguous write - keeps the header logic
        // simple regardless of how the payload write below gets chunked.
        Memory<byte> headerBuffer = writer.GetMemory(HeaderSize);
        WriteHeader(headerBuffer.Span, assetId, version, payload.Length);
        writer.Advance(HeaderSize);

        // Payload written in chunks sized to whatever the writer currently has
        // available, rather than assuming one contiguous buffer big enough for
        // the whole blob - keeps this workable for very large assets.
        ReadOnlyMemory<byte> remaining = payload;
        while (remaining.Length > 0)
        {
            Memory<byte> dest = writer.GetMemory(Math.Min(remaining.Length, 64 * 1024));
            int chunk = Math.Min(dest.Length, remaining.Length);
            remaining[..chunk].CopyTo(dest);
            writer.Advance(chunk);
            remaining = remaining[chunk..];
        }

        FlushResult result = await writer.FlushAsync(cancellationToken);
        if (result.IsCanceled) throw new OperationCanceledException(cancellationToken);
    }

    private static void WriteHeader(Span<byte> dest, int assetId, uint version, int payloadLength)
    {
        BitConverter.TryWriteBytes(dest[..4], assetId);
        BitConverter.TryWriteBytes(dest.Slice(4, 4), version);
        BitConverter.TryWriteBytes(dest.Slice(8, 4), payloadLength);
    }
}