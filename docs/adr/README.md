# Architecture Decision Records

One file per decision, numbered, never rewritten: a changed decision gets a **new** ADR that supersedes the old one.
Copy [`0000-template.md`](0000-template.md) to start a new one.

| # | Decision | Status |
|---|---|---|
| [0001](0001-single-writer-loop.md) | Concurrency: single-writer command loop | Accepted |
| [0002](0002-transport-abstraction.md) | Transport: `ITransport` abstraction, LiteNetLib adapter first, raw-socket adapter second | Accepted |
| [0003](0003-ulong-row-keys.md) | Typed tables use `ulong` row keys | Accepted |
| [0004](0004-event-log-and-snapshots.md) | Durability: append-only segmented log + snapshots | Accepted |
| [0005](0005-query-model.md) | Query model: cached compiled scans, then indexes; no remote expression trees | Accepted |
| [0006](0006-blob-leases.md) | Blob lifetime: ref-counted leases | Accepted |
| [0007](0007-build-test-conventions.md) | Build, test, and dependency conventions | **Proposed** |
| [0008](0008-table-version-semantics.md) | Table semantics: table-wide revision stamps every row | Accepted (delegated) |
