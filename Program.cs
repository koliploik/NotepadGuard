using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NotepadGuard;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

        Logger.LogStartupVerdict();
        Logger.Log($"=== STARTUP v{version} — exe: {Environment.ProcessPath} ===");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.LogException("UnhandledException", e.ExceptionObject as Exception);
        Application.ThreadException += (_, e) =>
            Logger.LogException("ThreadException", e.Exception);

        // Distinguishes a Windows logoff/shutdown from a silent death.
        SystemEvents.SessionEnding += (_, e) =>
            Logger.Log($"SESSION-END: Windows is ending the session ({e.Reason})");
        SystemEvents.PowerModeChanged += (_, e) =>
            Logger.Log($"power mode changed: {e.Mode}");

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            Logger.Log("SHUTDOWN: process exiting");

        using var mutex = new Mutex(true, "NotepadGuard_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Logger.Log("another instance is already running — exiting");
            MessageBox.Show("NotepadGuard is already running.",
                "NotepadGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Application.Run(new TrayApp());
            Logger.Log("SHUTDOWN: message loop ended normally");
        }
        catch (Exception ex)
        {
            Logger.LogException("Main", ex);
            MessageBox.Show($"NotepadGuard crashed:\n{ex.Message}",
                "NotepadGuard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
