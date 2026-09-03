using System.IO;
using System.Text.Json;

namespace lector_de_libros.Services;

/// <summary>
/// Persists, per book file path, the character offset (TextBox.CaretIndex) the reader's caret was at.
/// </summary>
public sealed class ReadingPositionStore
{
    private static readonly string SettingsFilePath = AppPaths.GetDataFilePath("reading-positions.json");

    private readonly Dictionary<string, int> _positionsByFilePath;

    public ReadingPositionStore()
    {
        _positionsByFilePath = Load();
    }

    public int? GetPosition(string filePath) =>
        _positionsByFilePath.TryGetValue(filePath, out int offset) ? offset : null;

    public void SetPosition(string filePath, int characterOffset)
    {
        _positionsByFilePath[filePath] = characterOffset;
        Save();
    }

    private static Dictionary<string, int> Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new Dictionary<string, int>();
            }
            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Dictionary<string, int>();
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
            string json = JsonSerializer.Serialize(_positionsByFilePath);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the reading position is best-effort; losing it isn't fatal.
        }
    }
}
