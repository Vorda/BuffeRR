# ADR-0001: Concurrency: single-writer command loop

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
BuffeRR stores `unmanaged` structs in dense arrays and hands out `Span<T>` views (zero-copy iteration, `AsBytes()` for
serialization). A span is invalidated by any structural change (growth, remove-swap, `ReplaceAll`). A UDP server is
inherently concurrent. Event sourcing additionally requires that replaying the same events yields the same state.

## Decision
All state mutation is performed by **one writer** consuming an ordered command queue (`Channel<TCommand>`, single
consumer). Commands are validated, turned into events, and applied by a **pure** `Apply(state, event)`. Time and
identifiers are injected by the loop and recorded in the event; `Apply` never reads clocks, randomness, or I/O.

Reads start as **queries executed on the writer loop**. A published immutable per-tick snapshot (double buffer or
copy-on-publish) is added only when a benchmark shows read pressure on the loop (see Validation).

## Alternatives considered
| Option | Pros | Cons |
|---|---|---|
| **Single-writer loop (chosen)** | Spans stay safe; deterministic replay; no locks on the hot path; simple mental model | One core caps write throughput; needs a read-publication strategy |
| Sharded tables, one writer per shard | Scales writes across cores | Cross-shard operations and ordering are hard; determinism harder |
| Locks / `ReaderWriterLockSlim` | Easiest to write | Spans cannot be held across mutation; contention; undermines the zero-copy design |

## Consequences
- Public API is command/query based; no public mutable access to table memory from other threads.
- `Apply` purity is a testable invariant (replay determinism test, M2).
- Backpressure policy for a full command queue must be designed (bounded channel; reject vs. wait).
- The legacy `Apply(key, Func<T,T>)` using `DateTime.UtcNow` inside the lambda is **not** compatible with this ADR.

## Validation
- M2: replaying a randomized event stream produces byte-identical table memory.
- Revisit (consider sharding) if a benchmark shows the writer loop saturating a core before the network does.
