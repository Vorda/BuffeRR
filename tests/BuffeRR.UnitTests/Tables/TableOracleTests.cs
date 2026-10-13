using BuffeRR.Engine.Tables;

namespace BuffeRR.UnitTests.Tables;

/// <summary>
/// Differential test: the same random operation sequence is applied to <see cref="Table{TRow}"/> and to the naive
/// <see cref="TableModel"/>; after every step both must agree on content, versions and revision. Seeds are fixed, so any
/// failure reproduces exactly (the failure message names the seed and step).
/// </summary>
public sealed class TableOracleTests
{
    private const int Seeds = 200;
    private const int StepsPerSeed = 600;
    private const int KeySpace = 48; // small on purpose: forces overwrites, removes of present keys, and re-adds

    [Fact]
    public void Random_operation_sequences_match_the_model()
    {
        for (var seed = 0; seed < Seeds; seed++)
            RunSequence(seed);
    }

    private static void RunSequence(int seed)
    {
        var random = new Random(seed);
        var table = new Table<TestRow>(initialCapacity: random.Next(0, 8));
        var model = new TableModel();

        for (var step = 0; step < StepsPerSeed; step++)
        {
            var where = $"seed {seed}, step {step}";
            var key = (ulong)random.Next(KeySpace);

            switch (random.Next(100))
            {
                case < 35:
                {
                    var row = new TestRow(random.NextInt64(), random.Next());
                    Assert.Equal(model.Set(key, row), table.Set(key, row));
                    break;
                }
                case < 55:
                {
                    var amount = random.Next(1, 100);
                    Assert.Equal(model.TryUpdate(key, amount), table.TryUpdate(key, new AddToA(amount)));
                    break;
                }
                case < 75:
                    Assert.Equal(model.Remove(key), table.Remove(key));
                    break;
                case < 85:
                {
                    var amount = random.Next(1, 100);
                    model.Transform(amount);
                    table.Transform(new AddToA(amount));
                    break;
                }
                case < 92:
                {
                    var entries = RandomUniqueEntries(random);
                    model.ReplaceAll(entries);
                    table.ReplaceAll(entries.ToArray());
                    break;
                }
                case < 96:
                {
                    // Two entries sharing a key that is guaranteed to be in the input twice, whatever else is there.
                    const ulong duplicateKey = 1_000_000UL;
                    var duplicated = RandomUniqueEntries(random)
                        .Append((duplicateKey, new TestRow(0, 0)))
                        .Append((duplicateKey, new TestRow(1, 1)))
                        .ToArray();
                    Assert.Throws<ArgumentException>(() => { table.ReplaceAll(duplicated); });
                    break; // model untouched; the comparison below proves the table is too
                }
                default:
                    model.Clear();
                    table.Clear();
                    break;
            }

            AssertSameState(model, table, where);
        }
    }

    private static List<(ulong Key, TestRow Row)> RandomUniqueEntries(Random random)
    {
        var keys = new HashSet<ulong>();
        var count = random.Next(0, KeySpace);
        while (keys.Count < count)
            keys.Add((ulong)random.Next(KeySpace * 2));

        return keys.Select(k => (k, new TestRow(random.NextInt64(), random.Next()))).ToList();
    }

    private static void AssertSameState(TableModel model, Table<TestRow> table, string where)
    {
        Assert.True(model.Revision == table.Revision, $"revision differs ({where})");
        Assert.True(model.Count == table.Count, $"count differs ({where})");

        // Spans are parallel and consistent with lookups.
        var keys = table.Keys;
        var rows = table.Rows;
        var versions = table.Versions;
        Assert.True(keys.Length == table.Count && rows.Length == table.Count && versions.Length == table.Count,
            $"span lengths differ from Count ({where})");

        var seen = new HashSet<ulong>();
        for (var i = 0; i < keys.Length; i++)
        {
            Assert.True(seen.Add(keys[i]), $"duplicate key {keys[i]} in Keys ({where})");
            Assert.True(model.Entries.TryGetValue(keys[i], out var expected), $"unexpected key {keys[i]} ({where})");
            Assert.True(expected.Row == rows[i], $"row mismatch for key {keys[i]} ({where})");
            Assert.True(expected.Version == versions[i], $"version mismatch for key {keys[i]} ({where})");

            Assert.True(table.TryGet(keys[i], out var viaLookup) && viaLookup == rows[i], $"TryGet disagrees with Rows ({where})");
            Assert.True(table.TryGetVersion(keys[i], out var versionViaLookup) && versionViaLookup == versions[i],
                $"TryGetVersion disagrees with Versions ({where})");
        }

        // Every version is bounded by the revision (invariant 4 in ADR-0008).
        foreach (var version in versions)
            Assert.True(version >= 1 && version <= table.Revision, $"version out of range ({where})");

        // Absent keys really are absent.
        for (ulong key = 0; key < KeySpace * 2; key++)
            Assert.True(model.Entries.ContainsKey(key) == table.ContainsKey(key), $"ContainsKey({key}) differs ({where})");
    }
}
