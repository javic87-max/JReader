using System.Collections.ObjectModel;
using System.IO;
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
    private const string DefaultWindowTitle = "Lector de libros";

    private readonly BookLoaderFactory _bookLoaderFactory = new();

    public ReadingPositionStore ReadingPositionStore { get; } = new();

    public RecentFilesStore RecentFilesStore { get; } = new();

    private readonly BookCacheStore _bookCacheStore = new();

    public ObservableCollection<RecentFile> RecentFiles { get; } = new();

    [ObservableProperty]
    private LoadedBook? _currentBook;

    [ObservableProperty]
    private double _baseFontSize = 16;

    [ObservableProperty]
    private bool _isTocVisible = true;

    [ObservableProperty]
    private string _windowTitle = DefaultWindowTitle;

    [ObservableProperty]
    private bool _hasRecentFiles;

    public MainViewModel()
    {
        RefreshRecentFiles();
    }

    /// <summary>
    /// Opening a file the user picked from disk is a trust boundary: any malformed EPUB/PDF can throw,
    /// and a parse failure must never crash the app, so exceptions are caught broadly here.
    /// </summary>
    public bool TryLoadBook(string filePath, out string? errorMessage)
    {
        try
        {
            LoadedBook book = _bookLoaderFactory.Load(filePath);
            CurrentBook = book;
            WindowTitle = $"{DefaultWindowTitle} — {book.Title}";
            _bookCacheStore.Save(book);
            RegisterRecent(filePath, book.Title);
            errorMessage = null;
            return true;
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
                errorMessage = null;
                return true;
            }

            errorMessage = $"No se pudo abrir «{Path.GetFileName(filePath)}»: {ex.Message}";
            return false;
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

    [RelayCommand]
    private void IncreaseFontSize() => BaseFontSize = Math.Min(MaxFontSize, BaseFontSize + FontSizeStep);

    [RelayCommand]
    private void DecreaseFontSize() => BaseFontSize = Math.Max(MinFontSize, BaseFontSize - FontSizeStep);

    [RelayCommand]
    private void ToggleToc() => IsTocVisible = !IsTocVisible;
}
