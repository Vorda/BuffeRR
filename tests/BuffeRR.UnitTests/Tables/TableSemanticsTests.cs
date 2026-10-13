using BuffeRR.Core.Tables;
using BuffeRR.Engine.Tables;

namespace BuffeRR.UnitTests.Tables;

/// <summary>One test per rule in ADR-0008.</summary>
public sealed class TableSemanticsTests
{
    private static readonly TestRow RowA = new(1, 1);
    private static readonly TestRow RowB = new(2, 2);

    [Fact]
    public void Set_adds_a_new_row_stamped_with_the_new_revision()
    {
        var table = new Table<TestRow>();

        var added = table.Set(10, RowA);

        Assert.True(added);
        Assert.Equal(1, table.Count);
        Assert.Equal(1UL, table.Revision);
        Assert.True(table.TryGet(10, out var row));
        Assert.Equal(RowA, row);
        Assert.True(table.TryGetVersion(10, out var version));
        Assert.Equal(1UL, version);
    }

    [Fact]
    public void Set_overwrites_an_existing_row_and_raises_its_version()
    {
        var table = new Table<TestRow>();
        table.Set(10, RowA);

        var added = table.Set(10, RowB);

        Assert.False(added);
        Assert.Equal(1, table.Count);
        Assert.True(table.TryGet(10, out var row));
        Assert.Equal(RowB, row);
        Assert.True(table.TryGetVersion(10, out var version));
        Assert.Equal(2UL, version);
    }

    [Fact]
    public void Set_with_an_identical_value_still_counts_as_a_change()
    {
        var table = new Table<TestRow>();
        table.Set(10, RowA);

        table.Set(10, RowA);

        Assert.Equal(2UL, table.Revision);
        Assert.True(table.TryGetVersion(10, out var version));
        Assert.Equal(2UL, version);
    }

    [Fact]
    public void Operations_that_find_nothing_do_not_change_the_revision()
    {
        var table = new Table<TestRow>();
        table.Set(10, RowA);

        Assert.False(table.Remove(99));
        Assert.False(table.TryUpdate(99, (ref TestRow r) => r.A++));
        Assert.False(table.TryUpdate(99, new AddToA(1)));

        Assert.Equal(1UL, table.Revision);
    }

    [Fact]
    public void Transform_and_Clear_on_an_empty_table_do_not_change_the_revision()
    {
        var table = new Table<TestRow>();

        table.Transform(new AddToA(1));
        table.Clear();

        Assert.Equal(0UL, table.Revision);
    }

    [Fact]
    public void Each_effective_mutation_raises_the_revision_by_exactly_one()
    {
        var table = new Table<TestRow>();

        table.Set(1, RowA);
        Assert.Equal(1UL, table.Revision);
        table.Set(2, RowA);
        Assert.Equal(2UL, table.Revision);
        table.TryUpdate(1, new AddToA(5));
        Assert.Equal(3UL, table.Revision);
        table.Transform(new AddToA(1));
        Assert.Equal(4UL, table.Revision);
        table.Remove(2);
        Assert.Equal(5UL, table.Revision);
        table.ReplaceAll([(7UL, RowB)]);
        Assert.Equal(6UL, table.Revision);
        table.Clear();
        Assert.Equal(7UL, table.Revision);
    }

    [Fact]
    public void A_removed_and_re_added_key_never_gets_an_older_version_back()
    {
        // The ABA problem the old per-row counter had: v1 -> removed -> re-added as v1 again.
        var table = new Table<TestRow>();
        table.Set(10, RowA);
        table.TryGetVersion(10, out var firstVersion);

        table.Remove(10);
        table.Set(10, RowA);
        table.TryGetVersion(10, out var secondVersion);

        Assert.True(secondVersion > firstVersion);
    }

    [Fact]
    public void Remove_moves_the_last_row_into_the_gap_and_keeps_its_key_row_and_version()
    {
        var table = new Table<TestRow>();
        table.Set(1, new TestRow(1, 0));
        table.Set(2, new TestRow(2, 0));
        table.Set(3, new TestRow(3, 0));
        table.TryGetVersion(3, out var versionOfLast);

        table.Remove(1);

        Assert.Equal(2, table.Count);
        Assert.False(table.ContainsKey(1));
        Assert.True(table.TryGet(3, out var moved));
        Assert.Equal(new TestRow(3, 0), moved);
        Assert.True(table.TryGetVersion(3, out var versionAfter));
        Assert.Equal(versionOfLast, versionAfter);
        Assert.True(table.TryGet(2, out var untouched));
        Assert.Equal(new TestRow(2, 0), untouched);
    }

    [Fact]
    public void Removing_the_last_row_works()
    {
        var table = new Table<TestRow>();
        table.Set(1, RowA);
        table.Set(2, RowB);

        Assert.True(table.Remove(2));

        Assert.Equal(1, table.Count);
        Assert.True(table.ContainsKey(1));
        Assert.False(table.ContainsKey(2));
    }

    [Fact]
    public void Transform_stamps_every_row_with_one_new_revision()
    {
        var table = new Table<TestRow>();
        table.Set(1, new TestRow(10, 0));
        table.Set(2, new TestRow(20, 0));

        table.Transform(new AddToA(1));

        Assert.Equal(3UL, table.Revision);
        Assert.True(table.TryGet(1, out var one));
        Assert.True(table.TryGet(2, out var two));
        Assert.Equal(11L, one.A);
        Assert.Equal(21L, two.A);
        foreach (var version in table.Versions)
            Assert.Equal(3UL, version);
    }

    [Fact]
    public void TryUpdate_mutates_in_place_and_stamps_only_that_row()
    {
        var table = new Table<TestRow>();
        table.Set(1, new TestRow(10, 0));
        table.Set(2, new TestRow(20, 0));

        Assert.True(table.TryUpdate(1, (ref TestRow r) => r.A += 5));

        Assert.True(table.TryGet(1, out var one));
        Assert.Equal(15L, one.A);
        Assert.True(table.TryGetVersion(1, out var v1));
        Assert.True(table.TryGetVersion(2, out var v2));
        Assert.Equal(3UL, v1);
        Assert.Equal(2UL, v2);
    }

    [Fact]
    public void Mutating_the_table_from_inside_a_callback_throws_and_the_table_stays_usable()
    {
        var table = new Table<TestRow>();
        table.Set(1, RowA);

        Assert.Throws<InvalidOperationException>(() =>
        {
            table.Transform((ref TestRow _) => table.Set(2, RowB));
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            table.TryUpdate(1, (ref TestRow _) => table.Remove(1));
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            table.Transform((ref TestRow _) => table.Clear());
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            table.Transform((ref TestRow _) => table.Transform(new AddToA(1)));
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            table.Transform((ref TestRow _) => table.ReplaceAll([(9UL, RowB)]));
        });

        // The guard flag must be reset: normal use works again and nothing was added or removed.
        Assert.True(table.Set(3, RowB));
        Assert.True(table.ContainsKey(1));
        Assert.False(table.ContainsKey(2));
    }

    [Fact]
    public void Reading_from_inside_a_callback_is_allowed()
    {
        var table = new Table<TestRow>();
        table.Set(1, new TestRow(5, 0));
        table.Set(2, new TestRow(7, 0));
        long sumSeen = 0;

        table.Transform((ref TestRow _) => sumSeen += table.Rows.Length);

        Assert.Equal(4L, sumSeen);
    }

    [Fact]
    public void A_throwing_callback_over_reports_the_change_and_leaves_the_table_usable()
    {
        var table = new Table<TestRow>();
        table.Set(1, RowA);
        table.TryGetVersion(1, out var before);

        Assert.Throws<NotSupportedException>(() =>
        {
            table.TryUpdate(1, (ref TestRow _) => throw new NotSupportedException());
        });

        Assert.True(table.TryGetVersion(1, out var after));
        Assert.True(after > before, "version must never under-report a possible change");
        Assert.True(table.Set(2, RowB));
    }

    [Fact]
    public void ReplaceAll_replaces_the_content_and_stamps_every_row_with_one_revision()
    {
        var table = new Table<TestRow>();
        table.Set(1, RowA);
        table.Set(2, RowA);

        table.ReplaceAll([(10UL, RowA), (20UL, RowB), (30UL, RowA)]);

        Assert.Equal(3, table.Count);
        Assert.False(table.ContainsKey(1));
        Assert.True(table.TryGet(20, out var row));
        Assert.Equal(RowB, row);
        Assert.Equal(3UL, table.Revision);
        foreach (var version in table.Versions)
            Assert.Equal(3UL, version);
    }

    [Fact]
    public void ReplaceAll_with_a_duplicate_key_throws_and_leaves_the_table_unchanged()
    {
        var table = new Table<TestRow>();
        table.Set(1, RowA);
        table.Set(2, RowB);
        var revisionBefore = table.Revision;

        Assert.Throws<ArgumentException>(() =>
        {
            table.ReplaceAll([(5UL, RowA), (6UL, RowB), (5UL, RowB)]);
        });

        Assert.Equal(2, table.Count);
        Assert.Equal(revisionBefore, table.Revision);
        Assert.True(table.TryGet(1, out var one));
        Assert.Equal(RowA, one);
        Assert.False(table.ContainsKey(5));
    }

    [Fact]
    public void ReplaceAll_can_empty_a_table_and_still_counts_as_one_revision()
    {
        var table = new Table<TestRow>();
        table.Set(1, RowA);

        table.ReplaceAll([]);

        Assert.Equal(0, table.Count);
        Assert.Equal(2UL, table.Revision);
    }

    [Fact]
    public void Clear_removes_everything()
    {
        var table = new Table<TestRow>();
        table.Set(1, RowA);
        table.Set(2, RowB);

        table.Clear();

        Assert.Equal(0, table.Count);
        Assert.False(table.ContainsKey(1));
        Assert.Equal(3UL, table.Revision);
    }

    [Fact]
    public void Growth_preserves_all_rows_keys_and_versions()
    {
        var table = new Table<TestRow>();

        for (ulong key = 0; key < 1000; key++)
            table.Set(key, new TestRow((long)key, (int)key));

        Assert.Equal(1000, table.Count);
        for (ulong key = 0; key < 1000; key++)
        {
            Assert.True(table.TryGet(key, out var row));
            Assert.Equal(new TestRow((long)key, (int)key), row);
            Assert.True(table.TryGetVersion(key, out var version));
            Assert.Equal(key + 1, version);
        }
    }

    [Fact]
    public void EnsureCapacity_prevents_reallocation_while_filling()
    {
        var table = new Table<TestRow>();
        table.EnsureCapacity(500);
        var capacity = table.Capacity;

        for (ulong key = 0; key < 500; key++)
            table.Set(key, RowA);

        Assert.Equal(capacity, table.Capacity);
    }

    [Fact]
    public void The_three_span_views_always_have_the_same_length_as_Count()
    {
        var table = new Table<TestRow>();
        for (ulong key = 0; key < 10; key++)
            table.Set(key, RowA);
        table.Remove(3);

        Assert.Equal(table.Count, table.Keys.Length);
        Assert.Equal(table.Count, table.Rows.Length);
        Assert.Equal(table.Count, table.Versions.Length);
    }

    [Fact]
    public void Argument_validation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = new Table<TestRow>(-1); });

        var table = new Table<TestRow>();
        Assert.Throws<ArgumentNullException>(() => { table.Transform((RowMutator<TestRow>)null!); });
        Assert.Throws<ArgumentNullException>(() => { table.TryUpdate(1, (RowMutator<TestRow>)null!); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { table.EnsureCapacity(-1); });
    }
}
