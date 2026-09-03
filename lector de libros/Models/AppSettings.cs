namespace lector_de_libros.Models;

public sealed record AppSettings(bool ReopenLastBookOnStartup = false, bool IsTocVisible = true);
