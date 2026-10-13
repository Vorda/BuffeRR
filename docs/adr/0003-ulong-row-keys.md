# ADR-0003: Typed tables use `ulong` row keys

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
The original README promised `ReadOnlySpan<byte>` keys; the prototype uses `ulong`. Supporting both in one table type
doubles the complexity of indexes, snapshots, and the wire format.

## Decision
`Table<T>` rows are keyed by `ulong`. Variable-length or string-like keys are out of scope for typed tables; if needed
they become a separate **KV table type** later (its own ADR).

## Alternatives considered
| Option | Pros | Cons |
|---|---|---|
| **`ulong` (chosen)** | Fixed-size, cheap hashing, trivial to put on the wire and in snapshots | Callers needing string keys must map to ids |
| `ReadOnlySpan<byte>` | Flexible, Redis-like | Variable-length storage, hashing cost, harder snapshots; conflicts with dense unmanaged rows |

## Consequences
- ID allocation is the caller's (or a command's) responsibility; the loop may inject ids for determinism (ADR-0001).
- README no longer claims byte-span keys.

## Validation
- Revisit if a concrete use case needs string keys and an id-mapping layer proves awkward.
