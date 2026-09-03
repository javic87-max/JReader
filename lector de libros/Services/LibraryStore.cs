using System.IO;
using System.Text.Json;
using lector_de_libros.Models;

namespace lector_de_libros.Services;

/// <summary>
/// Persists the user's manually curated library (books added on purpose, kept until explicitly
/// removed), sorted alphabetically by title. Unlike <see cref="RecentFilesStore"/> this has no
/// size cap and is never auto-pruned when a book is opened.
/// </summary>
public sealed class LibraryStore
{
    private static readonly string SettingsFilePath = AppPaths.GetDataFilePath("library.json");

    private readonly List<LibraryBook> _books;

    public LibraryStore()
    {
        _books = Load();
    }

    public IReadOnlyList<LibraryBook> Books => _books;

    public bool Contains(string filePath) =>
        _books.Any(b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

    public void Add(string filePath, string title)
    {
        _books.RemoveAll(b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        _books.Add(new LibraryBook(filePath, title));
        _books.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
        Save();
    }

    public void Remove(string filePath)
    {
        _books.RemoveAll(b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    private static List<LibraryBook> Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return [];
            }
            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<List<LibraryBook>>(json) ?? [];
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
            string json = JsonSerializer.Serialize(_books);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the library is best-effort; losing it isn't fatal.
        }
    }
}
