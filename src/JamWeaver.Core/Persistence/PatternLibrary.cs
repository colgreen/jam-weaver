using System.Collections.Immutable;
using System.Text;

namespace JamWeaver.Core.Persistence;

public sealed class PatternLibrary : IDisposable
{
    private readonly string _root;
    private readonly PatternJsonCodec _codec;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public PatternLibrary(string rootDirectory, PatternJsonCodec? codec = null, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _root = Path.GetFullPath(rootDirectory);
        _codec = codec ?? new PatternJsonCodec();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string RootDirectory => _root;

    public async Task<PatternLibraryEntry> SaveAsync(Pattern pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(_root);
            var existing = await FindByIdAsync(pattern.Id, cancellationToken).ConfigureAwait(false);
            if (existing.Count > 1)
                throw new PatternPersistenceException($"Pattern ID {pattern.Id.Value} exists in multiple library files.");
            var fileName = FileNameFor(pattern);
            var destination = ResolveFile(fileName);
            if (File.Exists(destination) && !existing.Any(path => path.Equals(destination, FileComparison)))
                throw new PatternPersistenceException($"Library target '{fileName}' is already used by another or invalid pattern.");
            var savedUtc = _timeProvider.GetUtcNow();
            var data = _codec.Encode(pattern, savedUtc);
            temporaryPath = ResolveFile($".{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, destination, true);
            temporaryPath = null;
            if (existing.Count == 1 && !Path.GetFileName(existing[0]).Equals(fileName, FileComparison))
                File.Delete(existing[0]);
            return EntryFrom(pattern, fileName, savedUtc);
        }
        catch (PatternPersistenceException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new PatternPersistenceException($"Could not save pattern '{pattern.Name}' in '{_root}': {ex.Message}", ex);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); } catch (IOException) { }
            }
            _saveLock.Release();
        }
    }

    public async Task<ImmutableArray<PatternLibraryEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_root)) return [];
        var entries = new List<PatternLibraryEntry>();
        foreach (var path in Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(await ReadEntryAsync(path, cancellationToken).ConfigureAwait(false));
        }

        var duplicates = entries.Where(entry => entry.IsValid && entry.Id is not null)
            .GroupBy(entry => entry.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Id is { } id && duplicates.Contains(id))
                entries[index] = entries[index] with { IsValid = false, Error = $"Duplicate pattern ID {id.Value}." };
        }
        return entries.OrderBy(entry => entry.Name ?? entry.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id?.Value).ThenBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
    }

    public async Task<Pattern> LoadAsync(PatternLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsValid) throw new PatternPersistenceException($"Cannot load invalid library entry '{entry.FileName}': {entry.Error}");
        var path = ResolveFile(entry.FileName);
        var decoded = await ReadDecodedAsync(path, cancellationToken).ConfigureAwait(false);
        if (entry.Id is { } expected && decoded.Pattern.Id != expected)
            throw new PatternPersistenceException($"Library entry '{entry.FileName}' changed since it was listed; run 'library' again.");
        return decoded.Pattern;
    }

    private async Task<List<string>> FindByIdAsync(PatternId id, CancellationToken cancellationToken)
    {
        var matches = new List<string>();
        if (!Directory.Exists(_root)) return matches;
        foreach (var path in Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var decoded = await ReadDecodedAsync(path, cancellationToken).ConfigureAwait(false);
                if (decoded.Pattern.Id == id) matches.Add(path);
            }
            catch (PatternPersistenceException) { }
        }
        return matches;
    }

    private async Task<PatternLibraryEntry> ReadEntryAsync(string path, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(path);
        try
        {
            var decoded = await ReadDecodedAsync(path, cancellationToken).ConfigureAwait(false);
            return EntryFrom(decoded.Pattern, fileName, decoded.SavedUtc);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is PatternPersistenceException or IOException or UnauthorizedAccessException)
        {
            return new PatternLibraryEntry(fileName, false, ex.Message, null, null, null, null, null, null, null);
        }
    }

    private async Task<DecodedPattern> ReadDecodedAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new PatternPersistenceException($"Library file '{Path.GetFileName(path)}' is a link/reparse point and will not be followed.");
            var length = new FileInfo(path).Length;
            if (length > PatternJsonCodec.MaximumDocumentBytes)
                throw new PatternPersistenceException($"Library file '{Path.GetFileName(path)}' exceeds the {PatternJsonCodec.MaximumDocumentBytes}-byte limit.");
            var data = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return _codec.Decode(data, $"Library file '{Path.GetFileName(path)}'");
        }
        catch (PatternPersistenceException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PatternPersistenceException($"Could not read library file '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
    }

    private string ResolveFile(string fileName)
    {
        if (!fileName.Equals(Path.GetFileName(fileName), StringComparison.Ordinal))
            throw new PatternPersistenceException("Library filenames cannot contain a path.");
        var path = Path.GetFullPath(Path.Combine(_root, fileName));
        var relative = Path.GetRelativePath(_root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new PatternPersistenceException("Resolved library file lies outside the configured root.");
        return path;
    }

    private static PatternLibraryEntry EntryFrom(Pattern pattern, string fileName, DateTimeOffset savedUtc) =>
        new(fileName, true, null, pattern.Id, pattern.Name.Value, pattern.Mode, pattern.Role,
            pattern.TonalContext, pattern.Recipe?.Seed, savedUtc);

    private static string FileNameFor(Pattern pattern)
    {
        var builder = new StringBuilder();
        var pendingHyphen = false;
        foreach (var character in pattern.Name.Value.ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingHyphen && builder.Length > 0) builder.Append('-');
                if (builder.Length < 48) builder.Append(character);
                pendingHyphen = false;
            }
            else pendingHyphen = true;
            if (builder.Length >= 48) break;
        }
        var stem = builder.ToString().TrimEnd('-');
        if (stem.Length == 0) stem = "pattern";
        var shortId = pattern.Id.Value.ToString("N")[..8];
        return $"{stem}--{shortId}.json";
    }

    private static StringComparison FileComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public void Dispose() => _saveLock.Dispose();
}
