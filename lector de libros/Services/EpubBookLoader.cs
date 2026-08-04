using System.IO;
using System.Windows;
using System.Windows.Documents;
using lector_de_libros.Models;
using VersOne.Epub;
using VersOne.Epub.Options;

namespace lector_de_libros.Services;

public sealed class EpubBookLoader : IBookLoader
{
    private const double DefaultFontSize = 16.0;

    public bool CanLoad(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".epub", StringComparison.OrdinalIgnoreCase);

    public LoadedBook Load(string filePath)
    {
        EpubBook book = ReadBookLeniently(filePath);

        FlowDocument document = new();
        NameScope.SetNameScope(document, new NameScope());

        Dictionary<string, string> chapterAnchorsByFilePath = new();
        int chapterIndex = 0;
        foreach (EpubLocalTextContentFile chapterFile in book.ReadingOrder)
        {
            Block? firstBlock = HtmlToFlowDocumentConverter.AppendChapter(document, chapterFile.Content, DefaultFontSize);
            if (firstBlock is not null)
            {
                string anchorName = $"chapter_{chapterIndex}";
                firstBlock.Name = anchorName;
                document.RegisterName(anchorName, firstBlock);
                chapterAnchorsByFilePath[chapterFile.FilePath] = anchorName;
            }
            chapterIndex++;
        }

        List<TocEntry> toc = book.Navigation is null
            ? []
            : BuildTocEntries(book.Navigation, chapterAnchorsByFilePath);

        string? author = book.AuthorList.Count > 0 ? book.Author : null;
        return new LoadedBook(filePath, book.Title, author, document, toc);
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

    private static List<TocEntry> BuildTocEntries(List<EpubNavigationItem> items, Dictionary<string, string> chapterAnchorsByFilePath)
    {
        List<TocEntry> result = [];
        foreach (EpubNavigationItem item in items)
        {
            List<TocEntry> children = BuildTocEntries(item.NestedItems, chapterAnchorsByFilePath);

            string? anchor = null;
            if (item.HtmlContentFile is not null)
            {
                chapterAnchorsByFilePath.TryGetValue(item.HtmlContentFile.FilePath, out anchor);
            }

            if (anchor is null && children.Count == 0)
            {
                // Navigation entry doesn't resolve to any known chapter and has no children to fall back to.
                continue;
            }

            result.Add(new TocEntry(item.Title, anchor ?? children[0].AnchorName, children));
        }
        return result;
    }
}
