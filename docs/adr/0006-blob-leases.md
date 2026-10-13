# ADR-0006: Blob lifetime: ref-counted leases

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
The prototype `BlobStoreRR.Store` disposes the previous buffer immediately and `Get()` returns a raw
`ReadOnlyMemory<byte>`. An async send holding that memory across an `await` can read a buffer already returned to the
pool and reused: silent data corruption.

## Decision
`BlobStore.Acquire(id)` returns a **`BlobLease : IDisposable`** exposing `ReadOnlyMemory<byte>`. A buffer returns to the
pool only when the store has released it (replaced or removed) **and** every outstanding lease is disposed. Leases are
released deterministically by the sender when the transport has finished with the data.

## Alternatives considered
| Option | Pros | Cons |
|---|---|---|
| **Ref-counted leases (chosen)** | Correct under async sends; keeps pooling | Callers must dispose; leak risk if they forget |
| Copy on read | Trivially safe | Allocates and copies large assets, defeating the purpose |
| Never reuse buffers (GC-only) | Simplest | Large-object-heap churn |

## Consequences
- Adds lease-count tracking and a diagnostics counter for outstanding leases (to detect leaks in tests).
- Interacts with ADR-0001: the writer loop replaces blobs; sends happen outside it.

## Validation
- Test: replace a blob while a lease is outstanding; the lease's bytes remain unchanged until disposed.
- Test: all leases disposed at shutdown (leak detection).
