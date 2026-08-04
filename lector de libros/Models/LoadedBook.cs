using System.Windows.Documents;

namespace lector_de_libros.Models;

public sealed record LoadedBook(
    string FilePath,
    string Title,
    string? Author,
    FlowDocument Content,
    IReadOnlyList<TocEntry> Toc);
