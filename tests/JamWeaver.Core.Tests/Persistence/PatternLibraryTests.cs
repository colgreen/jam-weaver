using JamWeaver.Core.Persistence;
using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Tests.Persistence;

public sealed class PatternLibraryTests
{
    [Fact]
    public async Task Save_creates_library_and_load_round_trips()
    {
        using var directory = new TemporaryDirectory();
        var library = new PatternLibrary(directory.LibraryPath);
        var pattern = PatternJsonCodecTests.MelodicPattern();

        var saved = await library.SaveAsync(pattern, TestContext.Current.CancellationToken);
        var listed = await library.ListAsync(TestContext.Current.CancellationToken);
        var loaded = await library.LoadAsync(listed.Single(), TestContext.Current.CancellationToken);

        Assert.StartsWith("fixture--20000000", saved.FileName, StringComparison.Ordinal);
        Assert.Equal(pattern.Id, loaded.Id);
        Assert.Equal(pattern.Recipe!.Seed, loaded.Recipe!.Seed);
    }

    [Fact]
    public async Task Same_name_patterns_remain_distinct_and_listing_is_sorted()
    {
        using var directory = new TemporaryDirectory();
        var library = new PatternLibrary(directory.LibraryPath);
        var second = PatternJsonCodecTests.MelodicPattern(new PatternId(Guid.Parse("30000000-0000-0000-0000-000000000003")), "same");
        var first = PatternJsonCodecTests.MelodicPattern(new PatternId(Guid.Parse("10000000-0000-0000-0000-000000000001")), "Same");
        await library.SaveAsync(second, TestContext.Current.CancellationToken);
        await library.SaveAsync(first, TestContext.Current.CancellationToken);

        var entries = await library.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, entries.Length);
        Assert.Equal(first.Id, entries[0].Id);
        Assert.NotEqual(entries[0].FileName, entries[1].FileName);
    }

    [Fact]
    public async Task Saving_renamed_same_id_replaces_old_filename()
    {
        using var directory = new TemporaryDirectory();
        var library = new PatternLibrary(directory.LibraryPath);
        var pattern = PatternJsonCodecTests.MelodicPattern();
        var original = await library.SaveAsync(pattern, TestContext.Current.CancellationToken);

        var renamed = await library.SaveAsync(pattern.Rename(new PatternName("New Name")), TestContext.Current.CancellationToken);
        var entries = await library.ListAsync(TestContext.Current.CancellationToken);

        Assert.Single(entries);
        Assert.Equal("New Name", entries[0].Name);
        Assert.NotEqual(original.FileName, renamed.FileName);
        Assert.False(File.Exists(Path.Combine(directory.LibraryPath, original.FileName)));
    }

    [Fact]
    public async Task Malformed_file_does_not_hide_valid_entries()
    {
        using var directory = new TemporaryDirectory();
        var library = new PatternLibrary(directory.LibraryPath);
        await library.SaveAsync(PatternJsonCodecTests.MelodicPattern(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory.LibraryPath, "broken.json"), "{", TestContext.Current.CancellationToken);

        var entries = await library.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, entries.Length);
        Assert.Single(entries, entry => entry.IsValid);
        Assert.Single(entries, entry => !entry.IsValid && entry.Error is not null);
    }

    [Fact]
    public async Task Duplicate_ids_are_all_invalid_and_cannot_be_loaded()
    {
        using var directory = new TemporaryDirectory();
        var library = new PatternLibrary(directory.LibraryPath);
        var saved = await library.SaveAsync(PatternJsonCodecTests.MelodicPattern(), TestContext.Current.CancellationToken);
        File.Copy(Path.Combine(directory.LibraryPath, saved.FileName), Path.Combine(directory.LibraryPath, "duplicate.json"));

        var entries = await library.ListAsync(TestContext.Current.CancellationToken);

        Assert.All(entries, entry => Assert.False(entry.IsValid));
        await Assert.ThrowsAsync<PatternPersistenceException>(() => library.LoadAsync(entries[0], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Pre_cancelled_save_preserves_existing_file()
    {
        using var directory = new TemporaryDirectory();
        var library = new PatternLibrary(directory.LibraryPath);
        var pattern = PatternJsonCodecTests.MelodicPattern();
        var saved = await library.SaveAsync(pattern, TestContext.Current.CancellationToken);
        var path = Path.Combine(directory.LibraryPath, saved.FileName);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => library.SaveAsync(pattern.Rename(new PatternName("changed")), cancellation.Token));

        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(Directory.EnumerateFiles(directory.LibraryPath), file => file.EndsWith(".tmp", StringComparison.Ordinal));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _parent = Path.Combine(Path.GetTempPath(), "jam-weaver-tests");
        public TemporaryDirectory()
        {
            LibraryPath = Path.GetFullPath(Path.Combine(_parent, Guid.NewGuid().ToString("N")));
        }
        public string LibraryPath { get; }
        public void Dispose()
        {
            if (!Directory.Exists(LibraryPath)) return;
            var relative = Path.GetRelativePath(Path.GetFullPath(_parent), LibraryPath);
            if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal) || relative.Length != 32)
                throw new InvalidOperationException("Refusing to remove an unexpected test directory.");
            Directory.Delete(LibraryPath, true);
        }
    }
}
