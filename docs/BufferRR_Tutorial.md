BufferRR — Source Walkthrough + Tutorial

Purpose of this document

This file explains the project layout, the important types, and how to run and experiment with the demos. It is written for a developer who sees the repository for the first time and wants a hands-on, step-by-step path to learn the code by running and modifying it.

Quick repo map (files you will read first)

- MemoryCacheRR.cs — dense, unmanaged value cache useful for per-frame/many-object data.
- BlobStoreRR.cs — pool-backed blob storage for variable-length binary assets.
- BlobStreamWriterRR.cs / BlobStreamReaderRR.cs — framing and parsing utilities using System.IO.Pipelines.
- Demos/Demo03/Program.cs — a compact, runnable demonstration that ties the pieces together (mesh import, world population, streaming simulation, and pipeline round-trip).
- Demos/Demo01 and Demo02 — smaller demo helper types showing struct layout techniques and scene node shapes.

High-level architecture and mental model

- MemoryCacheRR<TValue> is optimized for large arrays of unmanaged structs (TValue : unmanaged). It stores a dense array of values + keys + version stamps and provides O(1) Set/TryGet/Remove plus span access for bulk operations.
- BlobStoreRR stores arbitrary-length binary blobs; each blob is stored in its own rented IMemoryOwner<byte> buffer from MemoryPool<byte>. Overwrites dispose the previous buffer so memory is reclaimed immediately.
- BlobStreamWriterRR writes a single framed unit to a PipeWriter or any pipe-backed transport; BlobStreamReaderRR reads and reconstructs frames from a PipeReader. The frame format is: [int AssetId][uint Version][int PayloadLength][payload bytes].
- The demos show typical usage: import/encode binary assets, store them in BlobStoreRR, maintain a large world of nodes in MemoryCacheRR, then stream required assets to a simulated client only when the client needs a version it doesn't already have.

Key types explained (what to call, what to expect)

1) MemoryCacheRR<TValue>
- Intended for: thousands of small, unmanaged structs updated frequently (per-frame game state, transforms, etc.).
- Important members:
  - Set(ulong key, in TValue value): insert or overwrite a keyed value. Overwrites bump a per-slot version counter.
  - TryGet(ulong key, out TValue value): fast lookup.
  - ReplaceAll(ReadOnlySpan<(ulong Key, TValue Value)>): bulk replace all entries in one pass (useful for a weekly rotation or wholesale reload).
  - AsSpan() / AsBytes(): zero-copy access to the live block for efficient iteration or serialization.
  - Transform(RefAction<TValue>): mutable single-pass update for vectorized per-frame changes.
- Notes: TValue must be unmanaged. Versions are per-slot long integers; use GetVersion(key) to read them for optimistic concurrency decisions.

2) BlobStoreRR
- Intended for: large, variable-size assets (meshes, textures) where each asset owns a rent-from-pool buffer.
- Important members:
  - Store(int id, ReadOnlySpan<byte> data, uint version): stores or replaces an asset. Replacing disposes the previous IMemoryOwner and returns its memory to the pool.
  - Get(int id): returns ReadOnlyMemory<byte> view onto the asset bytes.
  - TryGetVersion, GetByteLength, Count, TotalBytesResident.
  - IDisposable: Dispose returns all rented buffers.
- Notes: Because each blob owns its own rent, the implementation supports async transfer of ReadOnlyMemory across awaits.

3) BlobStreamWriterRR / BlobStreamReaderRR
- BlobStreamWriterRR.WriteFrameAsync writes header+payload into a PipeWriter in two steps: header (contiguous write) then payload streamed in chunks. This makes it work for large payloads without requiring a single contiguous allocation at the writer side.
- BlobStreamReaderRR.ReadFramesAsync consumes a PipeReader, handles segmented ReadOnlySequence<byte> buffers, copies the fixed-size header into a stackalloc span to parse values safely, and yields a ReadOnlySequence<byte> payload that may be multi-segment.
- Typical flow: server writes frames for changed assets, client reads frames and processes the payload (e.g., store to local cache or decode into mesh structures).

Running the demo (hands-on tutorial)

Prerequisites
- .NET 10 SDK installed.
- Visual Studio 2026 (solution provided) or dotnet CLI.

Build and run (CLI)
1) From the repository root (where BuffeRR.slnx sits):
   dotnet build

2) Run the demo program that demonstrates asset import, client streaming simulation, and a Pipe-based round-trip:
   dotnet run --project .\BuffeRR.csproj

If you prefer Visual Studio, open BuffeRR.slnx and run the project. The console program entry used in Demos/Demo03/Program.cs will execute.

What to expect when it runs (walkthrough of the Demo03 output)
- The program imports a small set of fake meshes (MeshImporterRR) and stores them in BlobStoreRR.
- It creates a MemoryCacheRR<StreamNodeRR> world with thousands of nodes referencing the assets.
- The program simulates a client in a small neighborhood and prints how many meshes/bytes the server needed to transfer to bring the client up to date.
- The demo then updates a mesh version on the server and simulates another frame showing only changed assets are transferred.
- Finally, the demo uses a Pipe to write one framed asset to an in-memory PipeWriter and reads it back via BlobStreamReaderRR to demonstrate framing correctness across segmented buffers.

Experimentation ideas (try these to learn the code)
- Change mesh counts / node counts in Demos/Demo03/Program.cs and rerun to see how memory and bytes transferred scale.
- Force a big asset (increase vertexCount) to exercise the chunked payload write path in BlobStreamWriterRR.
- Inspect pipeline segmentation: in Demo03, modify the writer to write smaller chunks to observe BlobStreamReaderRR's segmented-path logic (it will still reconstruct frames correctly).
- Add logging around MemoryCacheRR.Transform to visualize per-frame updates.

Extending the code (practical next steps)
- Add a persistent disk-backed BlobStore (DiskBlobStoreRR) by implementing the same public API as BlobStoreRR. Use file-backed MemoryMappedFiles or a simple directory-of-files mapping; keep Version semantics the same.
- Add metrics: instrument buffer sizes, counts, version-change rates. Export via a metrics library or simple console counters for now.
- Add a network transport adapter: replace the in-memory Pipe with actual TCP or UDP adapters using PipeReader.Create/ PipeWriter.Create over NetworkStream.

Common pitfalls and tips
- MemoryCacheRR<T> requires TValue : unmanaged. If you need managed references inside values, either switch to a different store or convert to a blob-based representation.
- BlobStoreRR returns ReadOnlyMemory<byte>. Do not call .Span and cross an await boundary with the Span — that risks invalid memory access. Instead pass the ReadOnlyMemory directly to async APIs (as the demo does).
- When adding persistence, ensure you maintain the same version semantics so existing demo logic that compares versions continues to work.

If you want
- I can update this tutorial to include a short step-by-step code change (for example: add a DiskBlobStoreRR implementation) and apply it in the workspace.
- I can add small unit tests that assert frame-roundtrip behavior or MemoryCacheRR's ReplaceAll semantics.

