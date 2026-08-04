namespace lector_de_libros.Models;

public sealed record TocEntry(string Title, string AnchorName, IReadOnlyList<TocEntry> Children);
