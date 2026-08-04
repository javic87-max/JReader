using System.IO;
using System.Windows.Documents;
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

    [ObservableProperty]
    private LoadedBook? _currentBook;

    [ObservableProperty]
    private double _baseFontSize = 16;

    [ObservableProperty]
    private bool _isTocVisible = true;

    [ObservableProperty]
    private string _windowTitle = DefaultWindowTitle;

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
            RescaleHeadings(book.Content, BaseFontSize);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"No se pudo abrir «{Path.GetFileName(filePath)}»: {ex.Message}";
            return false;
        }
    }

    [RelayCommand]
    private void IncreaseFontSize() => BaseFontSize = Math.Min(MaxFontSize, BaseFontSize + FontSizeStep);

    [RelayCommand]
    private void DecreaseFontSize() => BaseFontSize = Math.Max(MinFontSize, BaseFontSize - FontSizeStep);

    [RelayCommand]
    private void ToggleToc() => IsTocVisible = !IsTocVisible;

    partial void OnBaseFontSizeChanged(double value)
    {
        if (CurrentBook is not null)
        {
            RescaleHeadings(CurrentBook.Content, value);
        }
    }

    private static void RescaleHeadings(FlowDocument document, double baseFontSize)
    {
        foreach (Block block in document.Blocks)
        {
            if (block is Paragraph { Tag: int level } paragraph &&
                HeadingStyle.LevelFontMultiplier.TryGetValue(level, out double multiplier))
            {
                paragraph.FontSize = baseFontSize * multiplier;
            }
        }
    }
}
