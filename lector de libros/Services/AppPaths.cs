using System.IO;

namespace lector_de_libros.Services;

/// <summary>
/// Central place for where the app stores its settings/library/cache data. Everything lives in a
/// "Data" folder next to the executable rather than in the user's AppData, so the whole app —
/// program plus all persisted state — stays inside a single portable folder.
/// </summary>
public static class AppPaths
{
    public static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

    public static string GetDataFilePath(string fileName) => Path.Combine(DataDirectory, fileName);
}
