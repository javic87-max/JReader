namespace lector_de_libros.Models;

public sealed record TocEntry(string Title, int CharacterOffset, IReadOnlyList<TocEntry> Children);
