using System.IO;
using System.Windows;
using System.Windows.Documents;
using lector_de_libros.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Outline;

namespace lector_de_libros.Services;

public sealed class PdfBookLoader : IBookLoader
{
    public bool CanLoad(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);

    public LoadedBook Load(string filePath)
    {
        using PdfDocument pdfDocument = PdfDocument.Open(filePath);

        FlowDocument document = new();
        NameScope.SetNameScope(document, new NameScope());

        Dictionary<int, string> pageAnchorsByPageNumber = new();

        foreach (Page page in pdfDocument.GetPages())
        {
            string anchorName = $"page_{page.Number}";
            Paragraph marker = new(new Run($"— Página {page.Number} —"))
            {
                FontStyle = FontStyles.Italic,
                Name = anchorName,
            };
            document.Blocks.Add(marker);
            document.RegisterName(anchorName, marker);
            pageAnchorsByPageNumber[page.Number] = anchorName;

            string pageText = ContentOrderTextExtractor.GetText(page);
            foreach (string paragraphText in SplitIntoParagraphs(pageText))
            {
                document.Blocks.Add(new Paragraph(new Run(paragraphText)));
            }
        }

        List<TocEntry> toc = [];
        if (pdfDocument.TryGetBookmarks(out Bookmarks? bookmarks) && bookmarks is not null)
        {
            toc = BuildTocEntries(bookmarks.Roots, pageAnchorsByPageNumber);
        }

        DocumentInformation info = pdfDocument.Information;
        string title = string.IsNullOrWhiteSpace(info.Title)
            ? Path.GetFileNameWithoutExtension(filePath)
            : info.Title;
        string? author = string.IsNullOrWhiteSpace(info.Author) ? null : info.Author;

        return new LoadedBook(filePath, title, author, document, toc);
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

    private static List<TocEntry> BuildTocEntries(IReadOnlyList<BookmarkNode> nodes, Dictionary<int, string> pageAnchorsByPageNumber)
    {
        List<TocEntry> result = [];
        foreach (BookmarkNode node in nodes)
        {
            List<TocEntry> children = BuildTocEntries(node.Children, pageAnchorsByPageNumber);

            string? anchor = null;
            if (node is DocumentBookmarkNode documentNode &&
                pageAnchorsByPageNumber.TryGetValue(documentNode.PageNumber, out string? found))
            {
                anchor = found;
            }

            if (anchor is null && children.Count == 0)
            {
                // Bookmark doesn't resolve to a known page (e.g. external link) and has no children to fall back to.
                continue;
            }

            result.Add(new TocEntry(node.Title, anchor ?? children[0].AnchorName, children));
        }
        return result;
    }
}
