namespace lector_de_libros.Models;

public sealed record UpdateInfo(
    Version LatestVersion,
    string TagName,
    string DownloadUrl,
    string AssetName,
    string ReleaseNotes);
