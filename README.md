# BuffeRR

**A typed, in-memory state engine for .NET 10: unmanaged struct tables, a deterministic single-writer core, event-sourced durability, and UDP state replication.**

> Status: **early design, M0 skeleton in place.** ADR-0001 to ADR-0006 are accepted ([`docs/adr/`](docs/adr/README.md)); ADR-0007 (build/test conventions) is proposed. This README is the source of truth for direction. Code that contradicts it is a bug in the code or a pending README change, never silent drift. Change the README first (see [Working agreement](#working-agreement)).

---

## 1. Why this exists

BuffeRR is a personal, educational, performance-obsessed project. The goals, in priority order:

1. **Learn and apply the modern .NET memory model** (`Span<T>`, `Memory<T>`, `ReadOnlySequence<T>`, `ref struct`, `stackalloc`, `System.IO.Pipelines`, pooling) on a problem where it actually matters.
2. **Build something coherent**, not a bag of demos: a small engine with a clear contract, tests, and benchmarks.
3. **Stay honest about performance**: every performance claim is backed by a BenchmarkDotNet result committed to the repo.

It is **not** aiming to be production infrastructure.

## 2. Identity (read this before proposing features)

BuffeRR is **not a Redis clone.** Redis stores untyped byte values behind a text/RESP command protocol. BuffeRR stores **typed, fixed-size, unmanaged structs in dense tables** and exposes them through a typed API and LINQ. The nearest relatives are an in-memory game-state/ECS store, a CQRS read-model host, and an in-memory relational core (such as SpacetimeDB) rather than Redis.

| BuffeRR **is** | BuffeRR **is not** |
|---|---|
| Typed tables of `unmanaged` structs | A generic key → byte[] store |
| Single-writer, deterministic command → event → state pipeline | A lock-based concurrent dictionary |
| Append-only event log + snapshots as the durability model | An RDB/AOF-compatible Redis |
| UDP-first replication with interest management (state/blob streaming to clients) | RESP-compatible (use Garnet for that) |
| LINQ-style typed queries and registered subscriptions | A string command parser |

**Prior art to study, not to reimplement:**
- **Garnet** (Microsoft Research): if you want a Redis-compatible store in .NET, use it. As of Oct 2026 its stable line is 2.1.x, targeting net10.0. BuffeRR must stay differentiated.
- **FASTER** (Microsoft): the log-structured KV store behind Garnet; the reference for hybrid log design.
- **LiteNetLib**: reliable-UDP library, the reference for channel and fragmentation semantics even if it is not adopted.
- **EventStoreDB / Marten**: event-sourcing semantics (streams, expected-version writes, projections).

## 3. Principles

1. **Single writer, many readers.** All mutation goes through one ordered command loop. This is what keeps spans safe, replay deterministic, and event sourcing honest.
2. **State is a fold of events.** `Apply(state, event) → state` is **pure**: no clocks, no randomness, no I/O. Time and IDs are injected by the loop and recorded in the event.
3. **Zero-copy where it pays, copies where it protects.** Spans never escape the owning thread/tick. Anything crossing an `await` is `Memory<T>` plus an explicit lease.
4. **Untrusted input is the default** for everything that arrives from a socket or a file: validate lengths, versions, and checksums before use.
5. **Explicit, fixed wire and disk formats.** Little-endian, versioned, schema-hashed. Never `BitConverter` or raw struct dumps without a layout contract.
6. **Ports and adapters.** The core knows nothing about sockets, files, or EF Core. Dependencies point inward.
7. **Measure first.** No optimization without a benchmark that shows the problem.

## 4. Architecture

```
            Client apps / SDK
                   │
        ┌──────────▼───────────┐
        │  Transport (adapters)│  UDP: LiteNetLib adapter, raw-socket adapter
        └──────────┬───────────┘
        ┌──────────▼───────────┐
        │  Protocol (codec)    │  datagram header, fragmentation, channels, acks
        └──────────┬───────────┘
        ┌──────────▼───────────┐
        │  Server host         │  sessions, interest management, subscriptions
        └──────────┬───────────┘
        ┌──────────▼───────────┐
        │  Engine (single      │  command queue → pure Apply → events
        │  writer loop)        │──► Tables<T>, BlobStore (leased), Indexes
        └──────────┬───────────┘
        ┌──────────▼───────────┐
        │  Persistence         │  event log (segments), snapshots, recovery
        └──────────────────────┘
```

### Core vocabulary

| Term | Meaning |
|---|---|
| **Table\<T\>** | Dense array of `T : unmanaged` plus keys and per-row version stamps. Evolution of today's `MemoryCacheRR<T>`. |
| **Command** | A request to change state (may be rejected). |
| **Event** | A fact that happened; the only thing persisted. Carries sequence number and injected timestamp. |
| **Apply** | Pure fold of an event into table state. Same log ⇒ byte-identical tables. |
| **Snapshot** | Point-in-time dump of tables, tagged with the last applied event sequence and a schema hash. |
| **Blob** | Large variable-size asset (mesh, texture) in the leased BlobStore, versioned, outside the per-tick path. |
| **Interest** | What a client currently cares about (for example, a proximity radius). The server sends only what changed within it. |

### Solution layout

```
BuffeRR.slnx
global.json  Directory.Build.props  Directory.Packages.props  .editorconfig
src/
  BuffeRR.Core/                 // abstractions + domain (ITable<T>, ICommand, IEvent, IEventLog, versions). NO references.
  BuffeRR.Engine/               // tables, blob store, single-writer loop, indexes          -> Core
  BuffeRR.Persistence/          // log segments, snapshots, recovery                        -> Core
  BuffeRR.Protocol/             // wire codecs (no sockets)                                 -> Core
  BuffeRR.Transport/            // ITransport abstraction only                              -> Core
  BuffeRR.Transport.LiteNetLib/ // (M5) first adapter; third-party package isolated here    -> Transport
  BuffeRR.Transport.RawSockets/ // (later) learning adapter                                 -> Transport
  BuffeRR.Server/               // host, DI wiring, sessions, interest management           -> Core, Engine, Persistence, Protocol, Transport
  BuffeRR.Client/               // client SDK                                               -> Core, Protocol, Transport
tests/
  BuffeRR.UnitTests/            // includes ArchitectureTests (dependency rule)
  BuffeRR.IntegrationTests/     // (M3/M5) crash/recovery, loopback UDP
  BuffeRR.Fuzz/                 // (M4) protocol + log parsers
benchmarks/
  BuffeRR.Benchmarks/           // BenchmarkDotNet
legacy/                         // exploratory prototype, not built; deleted as milestones supersede it
docs/adr/                       // one file per decision (see index)
docs/perf/                      // benchmark results worth keeping
```

## 5. Key design decisions (ADRs)

Each decision lives in its own file under [`docs/adr/`](docs/adr/README.md). Summaries below; the ADR file is authoritative.

### ADR-1: Concurrency model: *Accepted → single-writer loop* ([0001](docs/adr/0001-single-writer-loop.md))

| Option | Pros | Cons |
|---|---|---|
| **A. Single-writer command loop** (`Channel<Command>`, one consumer) | Spans stay safe. Deterministic replay. No locks on the hot path. Simple mental model. | One core is the write ceiling. Needs a read-publication strategy. |
| B. Sharded tables, one writer per shard | Scales writes across cores. | Cross-shard operations are hard. Determinism is harder. |
| C. Locks / `ReaderWriterLockSlim` | Easiest to write. | `AsSpan()` can't be held across mutation. Contention. Breaks the zero-copy story. |

**Read strategy (sub-decision):** (a) reads executed on the writer loop as queries, (b) a published immutable snapshot per tick (double buffer / copy-on-publish), or (c) both. Start with (a), add (b) when a benchmark justifies it.

### ADR-2: Transport: *Accepted → `ITransport` abstraction; LiteNetLib adapter first, raw-socket adapter second* ([0002](docs/adr/0002-transport-abstraction.md))

| Option | Pros | Cons |
|---|---|---|
| Raw `Socket`/`SocketAsyncEventArgs` only | Maximum learning, matches the original goal, full control. | You build reliability, fragmentation, congestion handling, and connection management before any feature works. |
| LiteNetLib only | Reliable/unreliable channels, fragmentation, connection handling are done. | Less learning. Third-party dependency. |
| **Abstraction + both** | Features can progress on LiteNetLib while the raw adapter is a bounded learning milestone. | Slightly more design up front. |

Maintenance check (Oct 2026): LiteNetLib has a 2.x line released in 2026, but NuGet listings I found disagreed on the exact latest version and target frameworks, so **verify at M5** (target framework, license, open issues) before depending on it.

**Note:** `System.IO.Pipelines` is a *stream* abstraction (TCP, in-memory, files). UDP is *message-oriented*, so it uses a datagram codec on `Span<byte>` with `ArrayPool`/`SocketAsyncEventArgs`, not a `PipeReader` frame parser. Pipelines stays in scope for the event-log reader, a future TCP transport, and blob streaming.

### ADR-3: Keys: *Accepted → `ulong` row keys for typed tables* ([0003](docs/adr/0003-ulong-row-keys.md))

Byte-span keys (`ReadOnlySpan<byte>`) belong to a separate, later **KV table type** for string-like keys. Mixing both in one table doubles the complexity of every index and snapshot.

### ADR-4: Durability: *Accepted → append-only segmented log + periodic snapshot* ([0004](docs/adr/0004-event-log-and-snapshots.md))

- Record layout: `[length u32][type u16][seq u64][timestamp i64][payload][crc32c u32]`, little-endian.
- Segments rotate by size; snapshots record `lastSeq` and a **schema hash** (struct layout fingerprint).
- Fsync policy is configurable: `EveryBatch` (group commit), `Interval(ms)`, `None`. Default `EveryBatch`.
- Recovery = latest valid snapshot + replay of the log tail. A torn or corrupt tail record is truncated, never trusted.
- Struct layouts are persisted, so every table type needs an explicit layout contract (`StructLayout`) and a schema version.

### ADR-5: Query model: *Accepted → compiled predicate scans, then indexes, no remote expression trees* ([0005](docs/adr/0005-query-model.md))

1. Compiled, **cached** `Expression<Func<T,bool>>` scans over spans (today's `Where` recompiles per call and copies values; fix both).
2. Secondary indexes: first a **spatial grid** (needed by interest management), then ordered/hash indexes.
3. A full `IQueryable` provider only if the typed API proves insufficient.
4. **Do not ship expression trees over the wire.** Remote clients use *registered* queries and subscriptions by name. Deserializing arbitrary expressions from a network peer is a security and complexity trap.

### ADR-6: Blob lifetime: *Accepted → ref-counted leases* ([0006](docs/adr/0006-blob-leases.md))

Replace `Get()` returning raw `ReadOnlyMemory<byte>` with a lease (`BlobLease : IDisposable`). The buffer is returned to the pool only when the store has released it **and** all leases are disposed. This fixes a use-after-return risk in the current `BlobStoreRR` (an overwrite during an in-flight async send).

## 6. Wire protocol sketch (UDP)

Not final; to be specified in `docs/protocol.md` during M4.

```
Datagram: [magic u16][protoVersion u8][flags u8][connectionId u32][seq u16][ack u16][ackBits u32][channel u8] [payload...]
Budget:   keep datagrams ≤ ~1200 bytes payload+header (conservative MTU); larger messages are fragmented
Channels: Reliable-Ordered (events, commands) · Unreliable-Sequenced (latest-wins state) · Blob (fragmented, resumable by version)
Delta:    per-row version stamps decide what to resend to a client within its interest set
```

All integers little-endian via `BinaryPrimitives`. All lengths validated against the datagram size before use.

## 7. Roadmap

Each milestone has **acceptance criteria**; a milestone is not done until they hold. Do them in order; M0 is not optional.

### M0: Foundations (do this first)
- [x] Solution split into the layout above; `DemoMode` csproj switching removed (skeleton provided).
- [ ] Move the prototype into `legacy/` (not built) and drop `DemoContacts` / the EF Core dependency.
- [x] `Directory.Build.props` / `Directory.Packages.props` (central package management), nullable on, analyzers on, warnings as errors in CI.
- [x] Test project (xunit.v3 on Microsoft Testing Platform, [ADR-0007](docs/adr/0007-build-test-conventions.md), still *Proposed*) with architecture tests; BenchmarkDotNet project (v0.15.x supports .NET 10).
- [x] CI workflow for Windows and Linux (`.github/workflows/ci.yml`).
- [ ] First CI run green on both OSes (the `src` projects were compile-checked on .NET 10.0.112 with `CI=true`; the test and benchmark projects were **not** restored or run yet).
- **Done when:** `dotnet build && dotnet test` is green on both OSes and the benchmark smoke run completes.

### M1: Table engine v2
- [ ] `Table<T>` with **consistent version semantics**: every mutation (`Set`, `TryUpdate`, `Transform`, remove) defines exactly what happens to versions; `ReplaceAll` and remove+re-add must not silently reset history without a documented rule.
- [ ] Expose keys alongside values (the current `AsSpan()` has no way to know which key a row belongs to).
- [ ] Define invalidation rules for spans in the API (documented and tested).
- [ ] Property-based tests against a `Dictionary` oracle; baseline benchmarks (Set/Get/Transform/scan, 1k/100k/1M rows).
- **Done when:** oracle tests pass for random operation sequences and a baseline benchmark report is committed.

### M2: Command/Event core
- [ ] Single-writer loop (ADR-1), `ICommand`/`IEvent`, pure `Apply`.
- [ ] Determinism test: replaying the same event stream yields **byte-identical** table memory.
- [ ] Time and IDs injected by the loop. (Today's Demo01 calls `DateTime.UtcNow` inside the `Apply` lambda, which would make replay non-deterministic.)
- **Done when:** the determinism test passes under randomized event streams.

### M3: Persistence
- [ ] Log writer/reader (ADR-4), snapshots with schema hash, recovery.
- [ ] Crash tests: truncated tail, flipped bits, torn writes, schema mismatch on load.
- **Done when:** kill-during-write tests always recover to a consistent prefix of the event stream.

### M4: Protocol codec
- [ ] Datagram codec, fragmentation/reassembly, channel semantics, `docs/protocol.md`.
- [ ] Fuzz tests on all parsers; malformed input never throws unhandled exceptions or allocates unboundedly.
- **Done when:** the fuzz corpus runs clean in CI.

### M5: Transport and replication
- [ ] `ITransport`; LiteNetLib adapter (verify per ADR-2); loopback integration tests.
- [ ] Interest management with a spatial index; version-based delta; blob streaming with leases (ADR-6).
- [ ] Raw-socket adapter as a learning milestone (optional; behind the same interface).
- **Done when:** a client in a simulated world receives only changed rows/assets within its interest radius, under simulated packet loss and reordering.

### M6: Query and indexes
- [ ] Cached compiled scans, spatial index, one ordered index, registered queries/subscriptions.

### M7: Hardening
- [ ] Soak tests, allocation budgets (gen0 collections per second under load), documented performance numbers, a public API review.

### Later (not scheduled)
Replication between nodes, clustering, pub/sub, memory-mapped persistence, WASM client.

## 8. Engineering standards

- **Clean, maintainable code over clever code.** Prefer SOLID and explicit dependencies; unsafe/`MemoryMarshal` tricks must be isolated behind small, well-tested types with a comment explaining the invariant.
- **Every public type has tests.** Every performance claim has a benchmark in `benchmarks/` and a result recorded in `docs/perf/`.
- **Performance budgets are set after M1 baselines, not guessed now.**
- **Allocation discipline** is measured (`MemoryDiagnoser`), not assumed.

## 9. Carry-over defects from the exploratory code (backlog)

Found by review of the prototype; fix or consciously drop during M0–M3.

1. `BlobStoreRR.Store` disposes the old buffer immediately; in-flight `ReadOnlyMemory` from `Get()` can read recycled memory → ADR-6.
2. `BlobStreamReaderRR`: `PayloadLength` is unvalidated (negative or huge on untrusted input); header uses machine-endian `BitConverter`; no magic, version, or checksum.
3. `MemoryCacheRR`: not thread-safe; `Transform` doesn't bump versions; `ReplaceAll` and remove+re-add reset versions; `Where` recompiles the expression on every call and copies each row; `Apply` does two dictionary lookups.
4. `FoodTruckMenu.NewWeekMenu`: items 4–7 are all built from `item3` (copy/paste bug).
5. `SetName`: truncating at 32 bytes can cut a UTF-8 code point in half.
6. README promised `ReadOnlySpan<byte>` keys; code uses `ulong` → ADR-3.
7. `Demo03` "streaming" only counts bytes; the real pipe round-trip sends one asset. Replace with the M5 integration test.

## 10. Working agreement

- **README first.** Propose changes to direction here (PR or commit) before writing code that depends on them.
- **ADRs are append-only history.** Supersede, don't rewrite.
- **Unknowns are marked "verify", never guessed.** Third-party dependencies are checked for maintenance status, license, and target-framework compatibility at the milestone that introduces them.

## 11. Tech stack

.NET 10 · C# 14 · `System.IO.Pipelines` · `System.Buffers` · BenchmarkDotNet · UDP via LiteNetLib and/or raw sockets (ADR-2)

## 12. License

MIT
