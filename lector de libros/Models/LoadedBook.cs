namespace lector_de_libros.Models;

public sealed record LoadedBook(
    string FilePath,
    string Title,
    string? Author,
    string Text,
    IReadOnlyList<TocEntry> Toc);
