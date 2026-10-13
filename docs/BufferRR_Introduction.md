BufferRR — Project Introduction

Purpose

BufferRR is a .NET 10 project that provides buffering and retry capabilities for data-processing workflows. It accepts incoming work items, holds them in a buffer (memory or disk-backed), and processes them with retry/backoff policies so transient failures don’t drop data.

Who should read this

- New developers joining the project
- Engineers extending buffering, persistence, or retry policies
- Reviewers wanting a quick mental model before reading code

Core concepts

- Ingest: entry points that enqueue items for processing.
- Buffer: queue-like structure that stores items until workers process them.
- Worker: background loop or service that dequeues and processes items.
- Retry policy: rules that determine when and how to retry failed work.
- Persistence: optional mechanism to persist buffered items across restarts.

Quick start

1. Prerequisites: .NET 10 SDK, Visual Studio 2026 or dotnet CLI.
2. Build: open BuffeRR.slnx and build or run dotnet build in repo root.
3. Run: dotnet run --project <main-project-path> or run from Visual Studio.
4. Tests: run tests via Test Explorer or dotnet test in the test project folder.

Where to look first

1. Program.cs / Startup.cs — shows DI wiring, configuration, and logging.
2. Buffer implementations — understand enqueue/dequeue lifecycle.
3. Workers/Processors — see how items are executed and how failures are handled.
4. Retry/Policies — read backoff strategy and configuration options.
5. Tests — good source of expected behaviors and edge-cases.

Common tasks for contributors

- Add a new buffer implementation (implement the buffer interface)
- Add metrics (buffer size, processing latency, retry counts)
- Improve persistence and recovery for safer restarts
- Add integration tests to exercise failure and recovery scenarios

If you want, I can generate a more specific README that references exact files and types after scanning the repository structure.
