using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace NotepadGuard;

static class Program
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "NotepadGuard", "error.log");

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogError("UnhandledException", e.ExceptionObject as Exception);
        Application.ThreadException += (_, e) =>
            LogError("ThreadException", e.Exception);

        using var mutex = new Mutex(true, "NotepadGuard_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("NotepadGuard is already running.",
                "NotepadGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Application.Run(new TrayApp());
        }
        catch (Exception ex)
        {
            LogError("Main", ex);
            MessageBox.Show($"NotepadGuard crashed:\n{ex.Message}",
                "NotepadGuard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void LogError(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex}\n---\n");
        }
        catch { }
    }
}
