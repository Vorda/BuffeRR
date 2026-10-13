using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SoftwareRR.BuffeRR;

/// <summary>Header of one frame produced by BlobStreamWriter.</summary>
public readonly record struct BlobFrameHeaderRR(int AssetId, uint Version, int PayloadLength);

/// <summary>
/// Reads frames written by BlobStreamWriter back off any PipeReader.
///
/// The core thing this teaches vs. plain Span parsing: data arriving from a
/// real transport isn't guaranteed to land in one contiguous buffer. A
/// PipeReader hands you a ReadOnlySequence<byte>, which may be spread
/// across several non-contiguous memory segments (e.g. one frame's header
/// split across two network reads). Span-based code assumes contiguity;
/// ReadOnlySequence-based code has to handle the segmented case explicitly -
/// that's what the header copy into a stackalloc buffer below is for.
/// </summary>
public static class BlobStreamReaderRR
{
    public const int HeaderSize = BlobStreamWriterRR.HeaderSize;

    /// <summary>
    /// Reads frames until the pipe completes or cancellation is requested.
    /// onFrame receives the header and a zero-copy view of the payload -
    /// valid only for the duration of that callback.
    /// </summary>
    public static async Task ReadFramesAsync(
        PipeReader reader,
        Action<BlobFrameHeaderRR, ReadOnlySequence<byte>> onFrame,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadFrame(ref buffer, out var header, out var payload))
                onFrame(header, payload);

            // Tell the pipe what we consumed (buffer.Start, since we reslice
            // as we consume complete frames) vs. what we examined
            // (buffer.End, since we scanned to the end looking for one more
            // frame). This consumed/examined split is the backpressure
            // contract Pipelines is built around.
            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                if (buffer.Length > 0)
                    throw new InvalidOperationException("Pipe completed with a partial frame still buffered.");
                break;
            }
        }

        await reader.CompleteAsync();
    }

    private static bool TryReadFrame(
        ref ReadOnlySequence<byte> buffer,
        out BlobFrameHeaderRR header,
        out ReadOnlySequence<byte> payload)
    {
        header = default;
        payload = default;

        if (buffer.Length < HeaderSize) return false;

        Span<byte> headerBytes = stackalloc byte[HeaderSize];
        buffer.Slice(0, HeaderSize).CopyTo(headerBytes); // handles the segmented case - CopyTo works even if the source spans multiple segments

        int assetId = BitConverter.ToInt32(headerBytes[..4]);
        uint version = BitConverter.ToUInt32(headerBytes.Slice(4, 4));
        int payloadLength = BitConverter.ToInt32(headerBytes.Slice(8, 4));

        long frameLength = HeaderSize + payloadLength;
        if (buffer.Length < frameLength) return false; // full frame hasn't arrived yet - wait for more data

        header = new BlobFrameHeaderRR(assetId, version, payloadLength);
        payload = buffer.Slice(HeaderSize, payloadLength);
        buffer = buffer.Slice(frameLength);
        return true;
    }
}