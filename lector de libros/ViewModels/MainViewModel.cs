using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lector_de_libros.Models;
using lector_de_libros.Services;

namespace lector_de_libros.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const double MinFontSize = 10;
    private const double MaxFontSize = 40;
    private const double FontSizeStep = 2;
    private const string DefaultWindowTitle = "J Reader";

    private readonly BookLoaderFactory _bookLoaderFactory = new();

    public ReadingPositionStore ReadingPositionStore { get; } = new();

    public RecentFilesStore RecentFilesStore { get; } = new();

    public LibraryStore LibraryStore { get; } = new();

    public AppSettingsStore AppSettingsStore { get; } = new();

    private readonly BookCacheStore _bookCacheStore = new();

    public ObservableCollection<RecentFile> RecentFiles { get; } = new();

    public ObservableCollection<LibraryBook> LibraryBooks { get; } = new();

    [ObservableProperty]
    private LoadedBook? _currentBook;

    [ObservableProperty]
    private double _baseFontSize = 16;

    [ObservableProperty]
    private bool _isTocVisible;

    [ObservableProperty]
    private string _windowTitle = DefaultWindowTitle;

    [ObservableProperty]
    private bool _hasRecentFiles;

    [ObservableProperty]
    private bool _hasLibraryBooks;

    [ObservableProperty]
    private bool _reopenLastBookOnStartup;

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup;

    public MainViewModel()
    {
        _reopenLastBookOnStartup = AppSettingsStore.ReopenLastBookOnStartup;
        _isTocVisible = AppSettingsStore.IsTocVisible;
        _checkForUpdatesOnStartup = AppSettingsStore.CheckForUpdatesOnStartup;
        RefreshRecentFiles();
        RefreshLibrary();
    }

    public string? GetLastOpenedFilePath() =>
        RecentFilesStore.RecentFiles.Count > 0 ? RecentFilesStore.RecentFiles[0].FilePath : null;

    partial void OnReopenLastBookOnStartupChanged(bool value) =>
        AppSettingsStore.ReopenLastBookOnStartup = value;

    partial void OnIsTocVisibleChanged(bool value) =>
        AppSettingsStore.IsTocVisible = value;

    partial void OnCheckForUpdatesOnStartupChanged(bool value) =>
        AppSettingsStore.CheckForUpdatesOnStartup = value;

    /// <summary>
    /// Opening a file the user picked from disk is a trust boundary: any malformed EPUB/PDF can throw,
    /// and a parse failure must never crash the app, so exceptions are caught broadly here. Parsing
    /// runs on a background thread because a long PDF can take a long time to extract text from, and
    /// doing that on the UI thread makes the window appear frozen (and, on Windows, eventually
    /// "not responding") for the whole time it's working.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> TryLoadBookAsync(string filePath)
    {
        try
        {
            LoadedBook book = await Task.Run(() => _bookLoaderFactory.Load(filePath));
            CurrentBook = book;
            WindowTitle = $"{DefaultWindowTitle} — {book.Title}";
            _bookCacheStore.Save(book);
            RegisterRecent(filePath, book.Title);
            return (true, null);
        }
        catch (Exception ex)
        {
            // The real file couldn't be read (drive unplugged, file moved, etc.) - fall back to
            // whatever was cached from the last time this book was opened successfully, if any.
            LoadedBook? cached = _bookCacheStore.TryLoad(filePath);
            if (cached is not null)
            {
                CurrentBook = cached;
                WindowTitle = $"{DefaultWindowTitle} — {cached.Title} (copia sin conexión)";
                RegisterRecent(filePath, cached.Title);
                return (true, null);
            }

            return (false, $"No se pudo abrir «{Path.GetFileName(filePath)}»: {ex.Message}");
        }
    }

    public void RemoveRecentFile(string filePath)
    {
        RecentFilesStore.Remove(filePath);
        _bookCacheStore.Prune(RecentFilesStore.RecentFiles.Select(r => r.FilePath));
        RefreshRecentFiles();
    }

    [RelayCommand]
    private void ClearRecentFiles()
    {
        RecentFilesStore.Clear();
        _bookCacheStore.Prune(RecentFilesStore.RecentFiles.Select(r => r.FilePath));
        RefreshRecentFiles();
    }

    private void RegisterRecent(string filePath, string title)
    {
        RecentFilesStore.Register(filePath, title);
        _bookCacheStore.Prune(RecentFilesStore.RecentFiles.Select(r => r.FilePath));
        RefreshRecentFiles();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (RecentFile recentFile in RecentFilesStore.RecentFiles)
        {
            RecentFiles.Add(recentFile);
        }
        HasRecentFiles = RecentFiles.Count > 0;
    }

    /// <summary>
    /// Best-effort title for a book that isn't being opened, just added to the library. Falls back
    /// to the file name when parsing fails, since a broken/missing file shouldn't block shelving it.
    /// </summary>
    public string TryGetBookTitle(string filePath)
    {
        try
        {
            return _bookLoaderFactory.Load(filePath).Title;
        }
        catch (Exception)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }
    }

    public bool IsInLibrary(string filePath) => LibraryStore.Contains(filePath);

    public void AddToLibrary(string filePath)
    {
        LibraryStore.Add(filePath, TryGetBookTitle(filePath));
        RefreshLibrary();
    }

    public void RemoveFromLibrary(string filePath)
    {
        LibraryStore.Remove(filePath);
        RefreshLibrary();
    }

    private void RefreshLibrary()
    {
        LibraryBooks.Clear();
        foreach (LibraryBook book in LibraryStore.Books)
        {
            LibraryBooks.Add(book);
        }
        HasLibraryBooks = LibraryBooks.Count > 0;
    }

    [RelayCommand]
    private void IncreaseFontSize() => BaseFontSize = Math.Min(MaxFontSize, BaseFontSize + FontSizeStep);

    [RelayCommand]
    private void DecreaseFontSize() => BaseFontSize = Math.Max(MinFontSize, BaseFontSize - FontSizeStep);

    [RelayCommand]
    private void ToggleToc() => IsTocVisible = !IsTocVisible;

    public void CloseBook()
    {
        CurrentBook = null;
        WindowTitle = DefaultWindowTitle;
    }
}
