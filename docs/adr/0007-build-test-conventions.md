# ADR-0007: Build, test, and dependency conventions

- **Status:** Proposed (not yet confirmed by the project owner)
- **Date:** 2026-10-13

## Context
M0 needs conventions that make later milestones cheap and keep the architecture enforceable.

## Decision (proposed)
1. **Layout & dependency rule:** `src/` projects reference inward only; Core has no project or package references.
   Third-party adapters (for example LiteNetLib) live in separate projects. Enforced by `ArchitectureTests`.
2. **Namespaces** use the assembly name as root (`BuffeRR.Core`, `BuffeRR.Engine`, ...), replacing the prototype's
   `SoftwareRR.BuffeRR`.
3. **Central package management** (`Directory.Packages.props`); projects reference packages without versions.
4. **Test framework:** xunit.v3 on Microsoft Testing Platform, selected via `global.json` (`test.runner`). No
   `Microsoft.NET.Test.Sdk` / VSTest adapter.
5. **Benchmarks:** BenchmarkDotNet in `benchmarks/`; keep results worth citing in `docs/perf/`.
6. **Warnings:** `TreatWarningsAsErrors` when `CI=true`; local builds report warnings without failing.
7. **Unsafe code** is disabled by default (`AllowUnsafeBlocks=false`); enabling it for a project needs a justification
   recorded in an ADR or in the project's README.

## Alternatives considered
| Option | Notes |
|---|---|
| NUnit / TUnit / MSTest instead of xunit.v3 | Viable; not evaluated in depth. Switch cost is low at M0, high later. |
| Warnings as errors everywhere | Stricter, but blocks local experimentation. |
| Single project with folders | What the prototype did; mixes EF Core into the core library and cannot enforce layering. |

## Consequences
- xunit.v3 test projects are executables and need the MTP runner configured in `global.json`.
- If the test setup misbehaves on first run, the fallback is the classic xunit.v3 + `Microsoft.NET.Test.Sdk` setup.

## Validation
- First CI run is green on Windows and Linux (M0 acceptance).
