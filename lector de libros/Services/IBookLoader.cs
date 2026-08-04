using lector_de_libros.Models;

namespace lector_de_libros.Services;

public interface IBookLoader
{
    bool CanLoad(string filePath);

    LoadedBook Load(string filePath);
}
