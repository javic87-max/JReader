using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using lector_de_libros.Models;

namespace lector_de_libros.Services;

/// <summary>
/// Caches the already-extracted text/TOC of opened books to disk, keyed by a hash of the source file
/// path, so a book stays readable from "Libros recientes" even if its original file is temporarily
/// unavailable (e.g. an external drive that isn't plugged in right now).
/// </summary>
public sealed class BookCacheStore
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LectorDeLibros",
        "cache");

    public void Save(LoadedBook book)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            string json = JsonSerializer.Serialize(book);
            File.WriteAllText(GetCacheFilePath(book.FilePath), json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Caching is best-effort; failing to write it just means no offline fallback later.
        }
    }

    public LoadedBook? TryLoad(string filePath)
    {
        try
        {
            string cacheFilePath = GetCacheFilePath(filePath);
            if (!File.Exists(cacheFilePath))
            {
                return null;
            }
            string json = File.ReadAllText(cacheFilePath);
            return JsonSerializer.Deserialize<LoadedBook>(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Deletes cached entries for files no longer in the given set (mirrors the recent-files cap).</summary>
    public void Prune(IEnumerable<string> filePathsToKeep)
    {
        try
        {
            if (!Directory.Exists(CacheDirectory))
            {
                return;
            }

            HashSet<string> keepFileNames = filePathsToKeep
                .Select(path => Path.GetFileName(GetCacheFilePath(path)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (string existingFile in Directory.EnumerateFiles(CacheDirectory, "*.json"))
            {
                if (!keepFileNames.Contains(Path.GetFileName(existingFile)))
                {
                    File.Delete(existingFile);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; leftover cache files aren't harmful.
        }
    }

    private static string GetCacheFilePath(string filePath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(filePath));
        string fileName = Convert.ToHexString(hash) + ".json";
        return Path.Combine(CacheDirectory, fileName);
    }
}
