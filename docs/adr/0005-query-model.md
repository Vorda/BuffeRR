# ADR-0005: Query model: cached compiled scans, then indexes; no remote expression trees

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
The README promises LINQ-style querying. The prototype's `Where(Expression<Func<T,bool>>)` recompiles the expression on
every call and copies each row. Accepting expression trees from remote peers would mean deserializing and compiling
untrusted code-shaped input.

## Decision
1. Local queries: **compiled predicates, cached** by expression shape, evaluated over table spans without copying rows.
2. **Indexes** in order of need: a spatial grid (required by interest management), then an ordered index and a hash index.
3. A full `IQueryable` provider is built **only if** the typed API proves insufficient.
4. **Remote clients never send expression trees.** They use named, server-registered queries and subscriptions.

## Alternatives considered
| Option | Pros | Cons |
|---|---|---|
| **Cached compiled scans + indexes (chosen)** | Fast, incremental, no new attack surface | LINQ surface is narrower than a full provider |
| Full `IQueryable` provider up front | Rich composition | Large effort before value; hard to optimize correctly |
| Serialized expression trees over the wire | Flexible remote queries | Security and versioning hazard; compile cost on server |

## Consequences
- Registered queries/subscriptions become part of the protocol and server host design (M5/M6).

## Validation
- M6: benchmarks comparing cached compiled scan vs. hand-written span loop (recorded in `docs/perf/`).
