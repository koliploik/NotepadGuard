using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NotepadGuard;

public record BackupEntry(string FilePath, string Label, DateTime Timestamp, long Size);

public class BackupManager
{
    private readonly string _backupRoot;
    private readonly Dictionary<string, string> _lastHashes = new();

    public string BackupRoot => _backupRoot;

    public BackupManager()
    {
        _backupRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "NotepadGuard", "backups");
        Directory.CreateDirectory(_backupRoot);
    }

    public bool SaveIfChanged(string windowTitle, string content)
    {
        content = NormalizeLineEndings(content);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

        if (_lastHashes.TryGetValue(windowTitle, out var prev) && prev == hash)
            return false;

        _lastHashes[windowTitle] = hash;

        var label = SanitizeLabel(windowTitle);
        if (string.IsNullOrEmpty(label)) label = "untitled";

        var folder = Path.Combine(_backupRoot, label);
        Directory.CreateDirectory(folder);

        var ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(folder, $"{ts}.txt");
        var n = 1;
        while (File.Exists(path))
            path = Path.Combine(folder, $"{ts}_{n++}.txt");

        File.WriteAllText(path, content, Encoding.UTF8);
        PruneOldBackups(folder, 200);
        return true;
    }

    public List<string> GetLabels()
    {
        if (!Directory.Exists(_backupRoot)) return [];
        return Directory.GetDirectories(_backupRoot)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList();
    }

    public List<BackupEntry> GetBackups(string label)
    {
        var folder = Path.Combine(_backupRoot, label);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder, "*.txt")
            .Select(f => new BackupEntry(f, label, File.GetLastWriteTime(f), new FileInfo(f).Length))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    public string ReadBackup(string filePath) => File.ReadAllText(filePath, Encoding.UTF8);

    private static string SanitizeLabel(string title)
    {
        if (title.StartsWith('*') || title.StartsWith('●'))
            title = title[1..].TrimStart();

        string[] suffixes = [" - Notepad", " - Blocco note", " - Bloc-notes", " - Bloc de notas", " - Editor de texto"];
        foreach (var s in suffixes)
        {
            var idx = title.LastIndexOf(s, StringComparison.OrdinalIgnoreCase);
            if (idx > 0) { title = title[..idx]; break; }
        }

        foreach (var c in Path.GetInvalidFileNameChars())
            title = title.Replace(c, '_');

        return title.Trim();
    }

    private static string NormalizeLineEndings(string text)
    {
        // UI Automation sometimes returns \r only — normalize to \r\n
        text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        return text;
    }

    private static void PruneOldBackups(string folder, int keep)
    {
        foreach (var f in Directory.GetFiles(folder, "*.txt")
            .OrderByDescending(File.GetLastWriteTime)
            .Skip(keep))
        {
            try { File.Delete(f); } catch { }
        }
    }
}
