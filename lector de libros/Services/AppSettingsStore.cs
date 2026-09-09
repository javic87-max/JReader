using System.IO;
using System.Text.Json;
using lector_de_libros.Models;

namespace lector_de_libros.Services;

/// <summary>
/// Persists user-configurable app preferences that aren't tied to a specific book.
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly string SettingsFilePath = AppPaths.GetDataFilePath("app-settings.json");

    private AppSettings _settings;

    public AppSettingsStore()
    {
        _settings = Load();
    }

    public bool ReopenLastBookOnStartup
    {
        get => _settings.ReopenLastBookOnStartup;
        set
        {
            if (_settings.ReopenLastBookOnStartup == value)
            {
                return;
            }
            _settings = _settings with { ReopenLastBookOnStartup = value };
            Save();
        }
    }

    public bool IsTocVisible
    {
        get => _settings.IsTocVisible;
        set
        {
            if (_settings.IsTocVisible == value)
            {
                return;
            }
            _settings = _settings with { IsTocVisible = value };
            Save();
        }
    }

    public bool CheckForUpdatesOnStartup
    {
        get => _settings.CheckForUpdatesOnStartup;
        set
        {
            if (_settings.CheckForUpdatesOnStartup == value)
            {
                return;
            }
            _settings = _settings with { CheckForUpdatesOnStartup = value };
            Save();
        }
    }

    private static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }
            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
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
            string json = JsonSerializer.Serialize(_settings);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting settings is best-effort; losing it isn't fatal.
        }
    }
}
