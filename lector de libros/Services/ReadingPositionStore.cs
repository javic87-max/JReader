using System.IO;
using System.Text.Json;
using System.Windows.Documents;

namespace lector_de_libros.Services;

/// <summary>
/// Persists, per book file path, how many plain-text characters into the FlowDocument the reader's
/// caret was. Restoring relies on the EPUB/PDF -> FlowDocument conversion being deterministic for the
/// same source file, so counting characters from the start is stable across sessions.
/// </summary>
public sealed class ReadingPositionStore
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LectorDeLibros",
        "reading-positions.json");

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

    public static int GetCharacterOffset(FlowDocument document, TextPointer position) =>
        new TextRange(document.ContentStart, position).Text.Length;

    public static TextPointer GetPositionAtCharacterOffset(FlowDocument document, int characterOffset)
    {
        TextPointer pointer = document.ContentStart;
        int remaining = characterOffset;

        while (pointer is not null)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                int runLength = pointer.GetTextRunLength(LogicalDirection.Forward);
                if (remaining <= runLength)
                {
                    return pointer.GetPositionAtOffset(remaining) ?? document.ContentEnd;
                }
                remaining -= runLength;
            }

            TextPointer? next = pointer.GetNextContextPosition(LogicalDirection.Forward);
            if (next is null)
            {
                break;
            }
            pointer = next;
        }

        return document.ContentEnd;
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
