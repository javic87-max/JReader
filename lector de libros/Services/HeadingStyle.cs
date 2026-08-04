namespace lector_de_libros.Services;

/// <summary>
/// Shared heading font-size scale, used both when converting EPUB HTML to a FlowDocument and when
/// the reader changes the base font size afterwards (heading paragraphs must be rescaled in step).
/// </summary>
public static class HeadingStyle
{
    public static readonly IReadOnlyDictionary<int, double> LevelFontMultiplier = new Dictionary<int, double>
    {
        [1] = 2.0,
        [2] = 1.7,
        [3] = 1.4,
        [4] = 1.2,
        [5] = 1.1,
        [6] = 1.0,
    };
}
