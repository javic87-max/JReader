using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using lector_de_libros.Models;

namespace lector_de_libros.Services;

/// <summary>
/// Downloads a new build and swaps it in for the running .exe. Windows won't let a running exe be
/// overwritten in place, so this hands off to a small helper batch script that waits for this
/// process to exit, moves the new file over the old one, and relaunches the app.
/// </summary>
public sealed class UpdateInstaller
{
    private const long MinimumValidDownloadBytes = 1_000_000;

    public async Task DownloadAndApplyAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        string currentExePath = Process.GetCurrentProcess().MainModule!.FileName!;
        string updateDirectory = Path.Combine(Path.GetTempPath(), "JReaderUpdate");
        Directory.CreateDirectory(updateDirectory);
        string newExePath = Path.Combine(updateDirectory, update.AssetName);

        using (HttpClient client = new() { Timeout = TimeSpan.FromMinutes(5) })
        using (Stream downloadStream = await client.GetStreamAsync(update.DownloadUrl, cancellationToken))
        using (FileStream fileStream = File.Create(newExePath))
        {
            await downloadStream.CopyToAsync(fileStream, cancellationToken);
        }

        if (new FileInfo(newExePath).Length < MinimumValidDownloadBytes)
        {
            throw new IOException("La actualización descargada parece incompleta.");
        }

        string scriptPath = Path.Combine(updateDirectory, "apply-update.bat");
        File.WriteAllText(scriptPath, BuildUpdateScript(Environment.ProcessId, newExePath, currentExePath));

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
        });

        Application.Current.Shutdown();
    }

    /// <summary>
    /// Polls for this process to disappear from tasklist rather than just waiting a fixed delay,
    /// since download/shutdown timing (and antivirus scanning of the new exe) can vary.
    /// </summary>
    private static string BuildUpdateScript(int processId, string newExePath, string currentExePath) =>
        $"""
        @echo off
        :wait
        tasklist /FI "PID eq {processId}" | find "{processId}" >nul
        if not errorlevel 1 (
            timeout /t 1 /nobreak >nul
            goto wait
        )
        move /y "{newExePath}" "{currentExePath}"
        start "" "{currentExePath}"
        del "%~f0"
        """;
}
