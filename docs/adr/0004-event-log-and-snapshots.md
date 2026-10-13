# ADR-0004: Durability: append-only segmented log + snapshots

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
State is a fold of events (ADR-0001). Durability should therefore persist events, with snapshots bounding recovery time.

## Decision
- **Record layout** (little-endian, via `BinaryPrimitives`):
  `[length u32][type u16][seq u64][timestamp i64][payload][crc32c u32]`.
- **Segments** rotate by size. Sequence numbers are gapless and monotonic.
- **Snapshots** record `lastSeq` and a **schema hash** (fingerprint of each table's struct layout). A snapshot whose
  schema hash does not match the running schema is rejected, never reinterpreted.
- **Fsync policy** is configurable: `EveryBatch` (group commit, default), `Interval(ms)`, `None`.
- **Recovery** = newest valid snapshot + replay of the log tail. A torn or corrupt tail record is truncated and never
  trusted; corruption before the tail is a hard error.
- Every persisted table type needs an explicit layout contract (`StructLayout`) and a schema version.

## Alternatives considered
| Option | Pros | Cons |
|---|---|---|
| **Event log + snapshots (chosen)** | Matches the fold model; replayable; streamable to other nodes later | Needs schema evolution story; recovery logic to test thoroughly |
| Periodic full dumps only | Simple | Loses data between dumps; no replay or audit |
| Embedded database (SQLite etc.) | Mature durability | Defeats the purpose; impedance mismatch with dense unmanaged tables |

## Consequences
- Struct layout changes are breaking for snapshots until a migration mechanism exists (a later ADR).
- Group commit couples the writer loop (ADR-0001) to batching decisions.

## Validation
- M3: crash tests (kill during write, truncated tail, flipped bits, torn writes, schema mismatch) always recover to a
  consistent prefix of the event stream.
