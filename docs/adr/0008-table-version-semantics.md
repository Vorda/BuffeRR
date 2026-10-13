# ADR-0008: Table semantics: table-wide revision stamps every row

- **Status:** Accepted (written under delegated authority for M1; supersede it if you disagree)
- **Date:** 2026-10-13

## Context
Delta replication (M5), event sourcing (ADR-0004) and optimistic concurrency all need one question answered
reliably: *"has this row changed since I last saw it?"*. The prototype used a per-row counter that
- reset to 1 on remove + re-add and on `ReplaceAll` (a client holding `(key, v1)` could never notice the row had been replaced: the ABA problem),
- was not bumped by `Transform`,
- returned `-1` as a "missing" sentinel,
and `ReplaceAll` silently accepted duplicate keys, leaving orphan rows in dense storage.

## Decision
The table owns a single **`Revision`** (`ulong`, starts at 0). **Every effective mutating call increments it by exactly
one**, and every row it touches is stamped with the new revision as that row's **`Version`**.

| Operation | Rows | Revision | Versions | Returns / errors |
|---|---|---|---|---|
| `Set(key, row)` new key | added | +1 | new row = new revision | `true` |
| `Set(key, row)` existing | overwritten (even if equal) | +1 | that row = new revision | `false` |
| `TryUpdate(key, mutator)` present | mutated in place | +1 | that row = new revision | `true` |
| `TryUpdate(key, mutator)` absent | none | unchanged | unchanged | `false` |
| `Remove(key)` present | removed (swap-remove) | +1 | moved row keeps its own version | `true` |
| `Remove(key)` absent | none | unchanged | unchanged | `false` |
| `Transform(mutator)` non-empty | all mutated in place | +1 (one pass = one revision) | **every** row = new revision | |
| `Transform(mutator)` empty | none | unchanged | n/a | |
| `ReplaceAll(entries)` | replaced wholesale | +1 (one operation = one revision, even if empty) | every row = new revision | duplicate key: `ArgumentException`, table untouched |
| `Clear()` non-empty | all removed | +1 | n/a | |
| `Clear()` empty | none | unchanged | n/a | |

### Invariants (tested by `TableSemanticsTests` and the differential `TableOracleTests`)
1. **Monotonic per key.** A key's version never decreases within a table's lifetime, including across remove + re-add
   and `ReplaceAll`. (Every stamp is a revision that is larger than all earlier ones.)
2. **Revision counts effective mutations.** Exactly +1 per call that finds something to do; no-ops change nothing.
3. **Versions may over-report change, never under-report it.** Stamps are written *before* a callback runs, so a
   throwing `RowMutator` leaves the row stamped. A delta consumer may re-send a row needlessly; it can never miss one.
4. **Bounded.** For every live row, `1 <= Version <= Revision`.
5. **`Set` always counts as a change**, even when the new value equals the old (comparing arbitrary unmanaged rows
   would cost a byte comparison per write; callers that care compare first).
6. **`Transform` is opaque.** The mutator may touch any field, so every row is stamped. Use `TryUpdate` per row when
   only some rows change and the distinction matters for replication.

### Storage, ordering and spans
- Rows, keys and versions are three parallel dense arrays; a dictionary maps key to slot.
- **Row order is unspecified** and changes on `Remove` (O(1) swap-remove).
- `Keys`, `Rows`, `Versions` are `ReadOnlySpan` views over live storage and are **valid only until the next mutating
  call** (growth, remove, `ReplaceAll` all invalidate them). Rows are read-only through spans; mutation goes through
  `Set` / `TryUpdate` / `Transform` so versions cannot be bypassed.
- **No mutation from inside a callback.** A mutator that called back into the table could trigger growth while a
  `ref` into the old array is live (silent lost writes). Mutating calls inside `TryUpdate`/`Transform` throw
  `InvalidOperationException`; reading is allowed.
- **Not thread-safe** (ADR-0001: one writer).
- **Exception safety:** `ReplaceAll` validates and builds its new index before touching state (strong guarantee).
  Growth allocates all three arrays before assigning any, so a failed allocation cannot desynchronize them.

### Two mutator forms
`RowMutator<T>` (delegate; convenient, captures allocate, no inlining) and `IRowMutator<T>` struct mutators passed to the
generic overloads (JIT specializes per struct type, so the call can inline; no allocation). The delegate overloads wrap
the delegate in a struct and use the same single implementation.

## Alternatives considered
| Option | Pros | Cons |
|---|---|---|
| Per-row counter (prototype) | Trivial | ABA on re-add / `ReplaceAll`; no ordering across rows; cannot answer "changed since X" |
| **Table-wide revision stamp (chosen)** | Total order across the table; "changed since R" is `Version > R`; survives remove/re-add; one-line mental model | 8 bytes/row; `Transform` stamps every row (an extra pass over `Versions`) |
| Caller supplies the revision (for example the event sequence number) | Versions become the event sequence: clients can say "I have everything up to seq N" | Couples table to the event pipeline; **deferred to M2**, see below |
| Compare-before-write to avoid false changes | Fewer replicated rows | Byte compare on every write; conflicts with the zero-overhead goal |

## Consequences
- **Snapshots (M3) must persist `Revision`** so versions stay monotonic after recovery.
- **M2 decision pending:** whether the engine's event sequence number *replaces* the table-local counter (injected
  revision) or stays separate. The table-local counter is deterministic under replay (same ops, same order), so M2 is not
  blocked, but the choice affects the wire protocol.
- **Deletes need tombstones for delta replication** ("row X was removed since revision R"). Not in M1: a removed row has
  no version to compare. Design in M5, with a bounded retention floor below which clients must full-resync.
- The dictionary index and its double hash operation on the insert path are the first optimization candidates; decide
  from benchmark results, not intuition.

## Validation
- `TableOracleTests`: 200 fixed seeds x 600 random operations, compared after every step against the naive
  `TableModel` (content, versions, revision, span consistency, absent keys).
- Mutation check performed when this ADR was written: six deliberately injected bugs (dropped version on swap-remove,
  missing revision bump on remove, `ReplaceAll` resetting versions to 1, `Transform` not stamping, duplicate keys
  accepted, missing re-entrancy guard) were each caught by at least one test.
- M1 benchmarks (`TableBuild`, `TableAccess`, `TableBulkMutation`) set the baseline in `docs/perf/`.
