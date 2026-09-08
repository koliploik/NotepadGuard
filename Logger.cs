using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NotepadGuard;

public static class Logger
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Sync = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "NotepadGuard", "notepadguard.log");

    public static void Log(string message)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                Rotate();
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{Environment.ProcessId}] {message}\r\n";
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
            catch { }
        }
    }

    public static void LogException(string source, Exception? ex)
        => Log($"EXCEPTION in {source}: {ex}");

    /// <summary>
    /// Marks the end of the previous run. If the last line of an existing log is not
    /// a clean shutdown marker, the previous instance died without exiting properly.
    /// </summary>
    public static void LogStartupVerdict()
    {
        try
        {
            if (!File.Exists(LogPath)) { Log("no previous log found — first run"); return; }

            string? lastLine = null;
            foreach (var l in File.ReadLines(LogPath))
                if (!string.IsNullOrWhiteSpace(l)) lastLine = l;

            if (lastLine == null) return;

            if (lastLine.Contains("SHUTDOWN") || lastLine.Contains("SESSION-END"))
                Log("previous run ended cleanly");
            else
                Log($"*** PREVIOUS RUN DID NOT SHUT DOWN CLEANLY — last activity was: {lastLine.Trim()}");
        }
        catch (Exception ex) { Log($"could not read previous log: {ex.Message}"); }
    }

    private static void Rotate()
    {
        try
        {
            var fi = new FileInfo(LogPath);
            if (!fi.Exists || fi.Length < MaxBytes) return;

            var old = LogPath + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(LogPath, old);
        }
        catch { }
    }

    public static void OpenInNotepad()
    {
        try
        {
            if (!File.Exists(LogPath)) Log("log opened (was empty)");
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{LogPath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }
}
