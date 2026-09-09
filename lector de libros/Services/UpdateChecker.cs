using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using lector_de_libros.Models;

namespace lector_de_libros.Services;

/// <summary>
/// Looks up the latest published GitHub release for J Reader and reports whether it's newer than
/// the running build. The app ships as a single portable .exe with no installer, so "newer" is
/// decided purely by comparing the assembly version against the release's tag name.
/// </summary>
public sealed class UpdateChecker
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/javic87-max/JReader/releases/latest";

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub's API rejects requests with no User-Agent header (403), regardless of auth.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JReader-UpdateChecker");

        using HttpResponseMessage response = await client.GetAsync(ReleasesApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement root = document.RootElement;

        string tagName = root.GetProperty("tag_name").GetString() ?? "";
        if (!TryParseVersion(tagName, out Version? latestVersion))
        {
            return null;
        }

        Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        if (latestVersion <= currentVersion)
        {
            return null;
        }

        JsonElement? assetElement = null;
        foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                assetElement = asset;
                break;
            }
        }

        if (assetElement is not { } foundAsset)
        {
            return null;
        }

        string assetName = foundAsset.GetProperty("name").GetString() ?? "J Reader.exe";
        string downloadUrl = foundAsset.GetProperty("browser_download_url").GetString() ?? "";
        string releaseNotes = root.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() ?? "" : "";

        return new UpdateInfo(latestVersion, tagName, downloadUrl, assetName, releaseNotes);
    }

    private static bool TryParseVersion(string tagName, [NotNullWhen(true)] out Version? version) =>
        Version.TryParse(tagName.TrimStart('v', 'V'), out version);
}
