using System.IO;
using System.Text.Json;
using lector_de_libros.Models;

namespace lector_de_libros.Services;

/// <summary>
/// Persists the most-recently-opened books (path + title), most recent first, capped at MaxEntries.
/// </summary>
public sealed class RecentFilesStore
{
    private const int MaxEntries = 10;

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LectorDeLibros",
        "recent-files.json");

    private readonly List<RecentFile> _recentFiles;

    public RecentFilesStore()
    {
        _recentFiles = Load();
    }

    public IReadOnlyList<RecentFile> RecentFiles => _recentFiles;

    public void Register(string filePath, string title)
    {
        _recentFiles.RemoveAll(r => string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        _recentFiles.Insert(0, new RecentFile(filePath, title));
        if (_recentFiles.Count > MaxEntries)
        {
            _recentFiles.RemoveRange(MaxEntries, _recentFiles.Count - MaxEntries);
        }
        Save();
    }

    public void Remove(string filePath)
    {
        _recentFiles.RemoveAll(r => string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public void Clear()
    {
        _recentFiles.Clear();
        Save();
    }

    private static List<RecentFile> Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return [];
            }
            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<List<RecentFile>>(json) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsFilePath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }
            string json = JsonSerializer.Serialize(_recentFiles);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the recent-files list is best-effort; losing it isn't fatal.
        }
    }
}
