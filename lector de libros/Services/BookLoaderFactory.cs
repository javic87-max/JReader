using System.IO;
using lector_de_libros.Models;

namespace lector_de_libros.Services;

public sealed class BookLoaderFactory
{
    private readonly IReadOnlyList<IBookLoader> _loaders =
    [
        new EpubBookLoader(),
        new PdfBookLoader(),
    ];

    public LoadedBook Load(string filePath)
    {
        IBookLoader? loader = _loaders.FirstOrDefault(l => l.CanLoad(filePath));
        if (loader is null)
        {
            throw new NotSupportedException($"Formato de archivo no soportado: {Path.GetExtension(filePath)}");
        }
        return loader.Load(filePath);
    }
}
