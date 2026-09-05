using System.IO;
using System.Text;
using lector_de_libros.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Tokens;

namespace lector_de_libros.Services;

public sealed class PdfBookLoader : IBookLoader
{
    public bool CanLoad(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);

    public LoadedBook Load(string filePath)
    {
        using PdfDocument pdfDocument = PdfDocument.Open(filePath);

        StringBuilder text = new();
        Dictionary<int, int> pageOffsetsByPageNumber = new();

        foreach (Page page in pdfDocument.GetPages())
        {
            if (text.Length > 0)
            {
                text.Append("\r\n\r\n");
            }
            pageOffsetsByPageNumber[page.Number] = text.Length;
            text.Append($"— Página {page.Number} —");

            string pageText = ContentOrderTextExtractor.GetText(page);
            foreach (string paragraphText in SplitIntoParagraphs(pageText))
            {
                text.Append("\r\n\r\n").Append(paragraphText);
            }
        }

        List<TocEntry> toc = [];
        if (pdfDocument.TryGetBookmarks(out Bookmarks? bookmarks) && bookmarks is not null)
        {
            toc = BuildTocEntries(bookmarks.Roots, pageOffsetsByPageNumber);
        }

        DocumentInformation info = pdfDocument.Information;
        string title = string.IsNullOrWhiteSpace(info.Title)
            ? Path.GetFileNameWithoutExtension(filePath)
            : info.Title;
        string? author = string.IsNullOrWhiteSpace(info.Author) ? null : info.Author;
        string? language = TryGetLanguage(pdfDocument);

        return new LoadedBook(filePath, title, author, text.ToString(), toc, language);
    }

    /// <summary>
    /// The document language (the catalog's /Lang entry) is optional in the PDF spec and most producers
    /// don't set it, so this is best-effort and simply returns null when absent or malformed.
    /// </summary>
    private static string? TryGetLanguage(PdfDocument pdfDocument)
    {
        if (pdfDocument.Structure.Catalog.CatalogDictionary.TryGet(NameToken.Create("Lang"), out StringToken? langToken) &&
            langToken is not null && !string.IsNullOrWhiteSpace(langToken.Data))
        {
            return langToken.Data;
        }
        return null;
    }

    private static IEnumerable<string> SplitIntoParagraphs(string pageText)
    {
        string[] rawParagraphs = pageText.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in rawParagraphs)
        {
            string collapsed = string.Join(' ', raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (collapsed.Length > 0)
            {
                yield return collapsed;
            }
        }
    }

    private static List<TocEntry> BuildTocEntries(IReadOnlyList<BookmarkNode> nodes, Dictionary<int, int> pageOffsetsByPageNumber)
    {
        List<TocEntry> result = [];
        foreach (BookmarkNode node in nodes)
        {
            List<TocEntry> children = BuildTocEntries(node.Children, pageOffsetsByPageNumber);

            int? offset = null;
            if (node is DocumentBookmarkNode documentNode &&
                pageOffsetsByPageNumber.TryGetValue(documentNode.PageNumber, out int found))
            {
                offset = found;
            }

            if (offset is null && children.Count == 0)
            {
                // Bookmark doesn't resolve to a known page (e.g. external link) and has no children to fall back to.
                continue;
            }

            result.Add(new TocEntry(node.Title, offset ?? children[0].CharacterOffset, children));
        }
        return result;
    }
}
