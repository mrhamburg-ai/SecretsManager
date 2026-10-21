using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SecretsManager.FileSystem.Internal;

/// <summary>
/// Handles all file I/O for the filesystem secret provider.
/// Provides per-key locking for thread safety and atomic writes via temp-file-then-rename.
/// </summary>
internal sealed partial class SecretFileStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Do not write null fields: optional values such as the key id and tags are omitted.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly char[] InvalidKeyChars =
        ['/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0'];

    private readonly string _basePath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set once <see cref="Dispose"/> starts. A key lock must never be handed out after that point:
    /// <see cref="Dispose"/> frees them, and a lock acquired afterwards would either be missed by
    /// that pass (leaking it) or freed underneath its new owner.
    /// </summary>
    private volatile bool _disposed;

    public SecretFileStore(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(basePath);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(basePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public async Task<VersionedSecretEnvelope> ReadEnvelopeAsync(string key, string version, CancellationToken ct)
    {
        var path = GetVersionPath(key, version);
        if (!File.Exists(path))
            throw new SecretNotFoundException(key);

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<VersionedSecretEnvelope>(json, JsonOptions)
            ?? throw new SecretProviderException($"Corrupt envelope for secret '{key}' version '{version}'.");
    }

    public async Task WriteEnvelopeAsync(string key, VersionedSecretEnvelope envelope, CancellationToken ct)
    {
        var dir = GetSecretDirectory(key);
        Directory.CreateDirectory(dir);

        var targetPath = GetVersionPath(key, envelope.Version);
        var tempPath = targetPath + ".tmp";

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    public async Task<string> ReadCurrentVersionAsync(string key, CancellationToken ct)
    {
        var path = GetCurrentPointerPath(key);
        if (!File.Exists(path))
            throw new SecretNotFoundException(key);

        var json = await File.ReadAllTextAsync(path, ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("version").GetString()
            ?? throw new SecretProviderException($"Invalid current pointer for secret '{key}'.");
    }

    public async Task WriteCurrentVersionAsync(string key, string version, CancellationToken ct)
    {
        var dir = GetSecretDirectory(key);
        Directory.CreateDirectory(dir);

        var targetPath = GetCurrentPointerPath(key);
        var tempPath = targetPath + ".tmp";

        var json = JsonSerializer.Serialize(new { version }, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    public string GetNextVersion(string key)
    {
        var dir = GetSecretDirectory(key);
        if (!Directory.Exists(dir))
            return "v1";

        var maxVersion = Directory.GetFiles(dir, "v*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(f => f != "current")
            .Select(f => int.TryParse(f!.AsSpan(1), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"v{maxVersion + 1}";
    }

    public List<string> GetVersions(string key)
    {
        var dir = GetSecretDirectory(key);
        if (!Directory.Exists(dir))
            return [];

        return Directory.GetFiles(dir, "v*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(f => f != "current")
            .Where(f => f is not null && f.Length > 1 && int.TryParse(f.AsSpan(1), out _))
            .OrderBy(f => int.Parse(f!.AsSpan(1)))
            .ToList()!;
    }

    public bool SecretExists(string key)
    {
        return File.Exists(GetCurrentPointerPath(key));
    }

    public void DeleteSecret(string key)
    {
        var dir = GetSecretDirectory(key);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    public SemaphoreSlim GetLock(string key)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SecretFileStore));

        return _locks.GetOrAdd(SanitizeKey(key), _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// Stops handing out locks. Safe to call while an operation is in flight.
    /// </summary>
    /// <remarks>
    /// Does not wait for the locks to come free, and does not free the ones already handed out.
    /// Waiting would make <c>Dispose</c> — called synchronously from <c>DisposeAsync</c> — hang
    /// behind an in-flight operation, and freeing a lock an operation still holds would make its
    /// <c>Release</c> throw <see cref="ObjectDisposedException"/>. Both problems are avoided by
    /// leaving the locks alive: the store is unreachable once disposed, so the lock objects are
    /// reclaimed with it, and an operation that arrives later is rejected by the
    /// <c>_disposed</c> check in <see cref="GetLock"/>.
    /// </remarks>
    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>
    /// Versions are always <c>v</c> followed by digits. Validating here closes traversal through
    /// the version query parameter, which previously reached the filesystem unchecked.
    /// </summary>
    [GeneratedRegex(@"^v[1-9][0-9]{0,9}$")]
    private static partial Regex VersionPattern();

    private string GetSecretDirectory(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Secret key must not be null or empty.", nameof(key));

        // '.' and '..' contain no separator, so SanitizeKey leaves them intact and Path.Combine
        // resolves them to the store root or its parent. Reject them explicitly.
        if (key is "." or "..")
            throw new ArgumentException(
                $"Invalid secret key '{key}': relative path segments are not allowed.", nameof(key));

        return EnsureUnderBase(Path.Combine(_basePath, SanitizeKey(key)));
    }

    private string GetVersionPath(string key, string version)
    {
        if (!VersionPattern().IsMatch(version))
            throw new ArgumentException(
                $"Invalid version '{version}': versions must match 'v<number>', for example 'v1'.",
                nameof(version));

        return EnsureUnderBase(Path.Combine(GetSecretDirectory(key), version + ".json"));
    }

    private string GetCurrentPointerPath(string key) =>
        EnsureUnderBase(Path.Combine(GetSecretDirectory(key), "current.json"));

    /// <summary>
    /// Canonicalises the path and confirms it is inside the store root. Belt-and-braces: the checks
    /// above should already prevent escape, but this makes the invariant explicit and local.
    /// </summary>
    private string EnsureUnderBase(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(_basePath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;

        if (!full.StartsWith(root, StringComparison.Ordinal))
            throw new ArgumentException($"Path '{full}' escapes the secret store root.");

        return full;
    }

    internal static string SanitizeKey(string key)
    {
        var sanitized = key;
        foreach (var c in InvalidKeyChars)
            sanitized = sanitized.Replace(c, '_');
        return sanitized;
    }
}
