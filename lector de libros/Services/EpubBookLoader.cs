using System.IO;
using System.Text;
using lector_de_libros.Models;
using VersOne.Epub;
using VersOne.Epub.Options;

namespace lector_de_libros.Services;

public sealed class EpubBookLoader : IBookLoader
{
    public bool CanLoad(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".epub", StringComparison.OrdinalIgnoreCase);

    public LoadedBook Load(string filePath)
    {
        EpubBook book = ReadBookLeniently(filePath);

        StringBuilder text = new();
        Dictionary<string, int> chapterOffsetsByFilePath = new();
        foreach (EpubLocalTextContentFile chapterFile in book.ReadingOrder)
        {
            chapterOffsetsByFilePath[chapterFile.FilePath] = text.Length;
            HtmlToPlainTextConverter.AppendChapter(text, chapterFile.Content);
        }

        List<TocEntry> toc = book.Navigation is null
            ? []
            : BuildTocEntries(book.Navigation, chapterOffsetsByFilePath);

        string? author = book.AuthorList.Count > 0 ? book.Author : null;
        return new LoadedBook(filePath, book.Title, author, text.ToString(), toc);
    }

    /// <summary>
    /// Real-world EPUB files are frequently non-conformant (mismatched manifest/nav entries, etc.).
    /// RELAXED tolerates the common cases; some files still fail even that, so IGNORE_ALL_ERRORS is
    /// used as a last-resort best-effort fallback (the table of contents may end up empty in that case).
    /// </summary>
    private static EpubBook ReadBookLeniently(string filePath)
    {
        try
        {
            return EpubReader.ReadBook(filePath, EpubReaderOptionsPreset.RELAXED)
                ?? throw new InvalidOperationException("El EPUB no se pudo leer ni siquiera en modo tolerante.");
        }
        catch (EpubReaderException)
        {
            return EpubReader.ReadBook(filePath, EpubReaderOptionsPreset.IGNORE_ALL_ERRORS)
                ?? throw new InvalidOperationException("El EPUB no se pudo leer ni siquiera en modo tolerante.");
        }
    }

    private static List<TocEntry> BuildTocEntries(List<EpubNavigationItem> items, Dictionary<string, int> chapterOffsetsByFilePath)
    {
        List<TocEntry> result = [];
        foreach (EpubNavigationItem item in items)
        {
            List<TocEntry> children = BuildTocEntries(item.NestedItems, chapterOffsetsByFilePath);

            int? offset = null;
            if (item.HtmlContentFile is not null &&
                chapterOffsetsByFilePath.TryGetValue(item.HtmlContentFile.FilePath, out int found))
            {
                offset = found;
            }

            if (offset is null && children.Count == 0)
            {
                // Navigation entry doesn't resolve to any known chapter and has no children to fall back to.
                continue;
            }

            result.Add(new TocEntry(item.Title, offset ?? children[0].CharacterOffset, children));
        }
        return result;
    }
}
