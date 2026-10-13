using System.Numerics;

namespace BuffeRR.Benchmarks;

/// <summary>Row shaped like a scene-graph node: realistic size (~28 bytes with padding) and contains a vectorizable field.</summary>
public struct NodeRow
{
    public ulong Id;
    public int MeshId;
    public Vector3 Position;
}

/// <summary>Deterministic data shared by the table benchmarks so runs are comparable.</summary>
public static class TableBenchmarkData
{
    public static (ulong Key, NodeRow Row)[] CreateEntries(int length)
    {
        var random = new Random(42);
        var entries = new (ulong, NodeRow)[length];
        for (var i = 0; i < length; i++)
        {
            var key = (ulong)(i + 1);
            entries[i] = (key, new NodeRow
            {
                Id = key,
                MeshId = random.Next(0, 64),
                Position = new Vector3(random.NextSingle() * 200 - 100, random.NextSingle() * 200 - 100, random.NextSingle() * 200 - 100),
            });
        }

        return entries;
    }

    /// <summary>All keys in a fixed pseudo-random order, so lookups are not a best-case sequential walk.</summary>
    public static ulong[] CreateShuffledKeys(int length)
    {
        var keys = new ulong[length];
        for (var i = 0; i < length; i++)
            keys[i] = (ulong)(i + 1);

        new Random(7).Shuffle(keys);
        return keys;
    }
}
