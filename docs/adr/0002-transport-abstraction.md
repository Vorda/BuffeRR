# ADR-0002: Transport: abstraction with LiteNetLib first, raw sockets second

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
The README targets low-latency UDP replication. Building reliability, ordering, fragmentation, and connection
management on raw sockets is a large project of its own and would delay every feature that sits above it, yet the
raw-socket work is also part of the learning goal.

## Decision
- The server and client depend on an **`ITransport` abstraction** (`BuffeRR.Transport`), never on a concrete library.
- Each implementation lives in **its own project** so third-party packages never leak into Core/Engine:
  1. `BuffeRR.Transport.LiteNetLib` (first, M5): maps channels to LiteNetLib delivery methods.
  2. `BuffeRR.Transport.RawSockets` (later, learning milestone): `Socket`/`SocketAsyncEventArgs`, same contract.
- The abstraction exposes **capabilities** (maximum message size, reliable/unreliable delivery, fragmentation provided
  or not) instead of assuming them, so the protocol layer does not duplicate what an adapter already guarantees.
- `System.IO.Pipelines` is a stream abstraction; it is **not** used for UDP framing. It remains in scope for the event log
  reader, blob streaming, and any future TCP transport.

## Alternatives considered
| Option | Pros | Cons |
|---|---|---|
| Raw sockets only | Maximum learning, full control | Reliability/fragmentation/congestion built before any feature works |
| LiteNetLib only | Fast to a working system | Less learning; hard dependency on one library |
| **Abstraction + both (chosen)** | Features progress on LiteNetLib while raw adapter is a bounded learning milestone | More design up front; abstraction must stay honest to both |

## Consequences
- The abstraction API is designed in M5 from two real consumers (server, client), not speculatively now.
- Adapter selection is a DI/composition-root concern in `BuffeRR.Server`.

## Validation
- **Verify before M5 starts:** LiteNetLib's current version, target frameworks (.NET 10 compatibility), license, and
  open-issue health. NuGet listings disagreed on the latest version when this was written.
- The same integration test suite (loopback, simulated loss and reordering) must pass against every adapter.
