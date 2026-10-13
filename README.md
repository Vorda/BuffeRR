
---

# **Project: BuffeRR**  
*A modern .NET‑native, high‑performance in‑memory data store inspired by Redis — built to explore .NET 10’s memory model, Span/Memory, and low‑level performance techniques.*

---

## **Overview**

BuffeRR is an experimental, educational, and performance‑driven project aimed at building a Redis‑style in‑memory data store using **pure .NET 10**, with a focus on:

- **Modern .NET memory primitives** — `Span<T>`, `Memory<T>`, `ReadOnlySpan<T>`, `stackalloc`, `ref struct`, and friends  
- **High‑performance UDP networking** built on years of real‑world game‑dev experience, optimized for low‑latency state replication and massive concurrency.  
- **Event‑sourced persistence** for durable storage and replay  
- **LINQ‑first query syntax** instead of Redis‑style command parsing  
- **Cross‑platform runtime** (Windows, Linux, containers) thanks to .NET 10’s unified hosting model

This project is intentionally low‑level, experimental, and performance‑obsessed. It’s designed for a seasoned .NET developer stepping into the modern memory‑centric part of .NET for the first time.

---

## **Goals**

### **Primary Goals**
- Build a **minimal Redis‑like memory store** using .NET 10 primitives  
- Learn and apply **Span/Memory** and related high‑performance APIs  
- Implement a **custom binary protocol** over UDP  
- Support **LINQ‑style querying** instead of string‑based commands  
- Add **event sourcing** for durable, append‑only persistence  
- Achieve **high throughput** and **low latency** under load

### **Secondary Goals**
- Explore .NET 10 runtime improvements  
- Experiment with zero‑allocation patterns  
- Build a modular architecture that can evolve into:  
  - A distributed cache  
  - A game‑server state backend  
  - A lightweight message broker  
  - A custom in‑memory database

---

## **Why This Project Exists**

With 20 years of .NET development under belt, this project is a deliberate dive into the parts of .NET 
that are 'new' for me, mostly **Span<T> and Memory<T>**.

I will also play with ideas like:

- **Pipelines**  
- **Binary serialization without allocations**  
- **High‑performance networking**  
- **Event sourcing as a persistence model**  
- **Custom protocol design**

It’s a playground for learning, benchmarking, and pushing .NET 10 to its limits.

---

## **Core Concepts**

### **1. Memory Layer (Span/Memory‑Driven)**
The memory layer is the heart of BuffeRR. It focuses on:

- Zero‑allocation reads/writes  
- Struct‑based storage  
- Custom memory pools  
- Efficient key/value storage using `ReadOnlySpan<byte>` keys  
- Optional typed storage using generics and `Memory<T>`

This is where the Redis‑like behavior emerges.

---

### **2. Networking Layer (UDP Game‑Dev Style)**

BuffeRR uses UDP for:

- Low latency  
- High throughput  
- Fire‑and‑forget semantics  
- Custom binary protocol  
- Optional reliability layer (ACKs, sequence IDs)

The networking layer is built using:

- `Socket` with `SocketAsyncEventArgs`  
- `System.IO.Pipelines`  
- Span‑based parsing and serialization

---

### **3. Query Layer (LINQ Instead of Commands)**

Instead of Redis commands like:

```
GET key
SET key value
```

BuffeRR exposes a strongly‑typed API:

```csharp
var result = store.Query<MyType>()
                  .Where(x => x.Score > 100)
                  .OrderByDescending(x => x.Score)
                  .Take(10)
                  .ToList();
```

This is powered by:

- Expression trees  
- Custom query providers  
- In‑memory indexing  
- Optional compiled queries

---

### **4. Event Sourcing Layer**

Persistence is append‑only:

- Every mutation becomes an event  
- Events are written to disk (or cloud storage)  
- Replay reconstructs the in‑memory state  
- Snapshots reduce replay time  
- Events can be streamed to other nodes

This makes BuffeRR durable and cluster‑friendly.

---

## **Architecture**

```
+---------------------------+
|        Client Apps        |
+---------------------------+
             |
             v
+---------------------------+
|     UDP Protocol Layer    |
+---------------------------+
             |
             v
+---------------------------+
|   Binary Serialization     |
|   (Span<T>/Memory<T>)     |
+---------------------------+
             |
             v
+---------------------------+
|       Memory Store        |
|  (Key/Value, Typed Data)  |
+---------------------------+
             |
             v
+---------------------------+
|     Event Sourcing        |
|   (Append-only storage)   |
+---------------------------+
```

---

## **Features (Planned)**

### **MVP**
- In‑memory key/value store  
- UDP server  
- Binary protocol  
- Basic LINQ querying  
- Event log + replay  
- Benchmarks

### **Future**
- Clustering  
- Replication  
- Snapshotting  
- Typed collections  
- Pub/Sub  
- Custom indexes  
- Memory‑mapped files  
- WASM client  
- Cloud‑native hosting

---

## **Tech Stack**

- **.NET 10**  
- **C# 13**  
- **Span/Memory APIs**  
- **System.IO.Pipelines**  
- **UDP sockets**  
- **Event sourcing patterns**  
- **BenchmarkDotNet**  
- **Docker** (optional)

---

## **Project Status**

This project is in **early exploration** mode.  
The goal is learning, experimenting, and building a foundation — not shipping a production‑ready Redis clone.

---

## **Contributions**

This is a personal learning project, but contributions, ideas, and discussions are welcome — especially around:

- Memory model improvements  
- Protocol design  
- Event sourcing patterns  
- LINQ provider architecture  
- Benchmarking strategies

---

## **License**

MIT — free to use, modify, and learn from.

---