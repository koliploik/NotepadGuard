using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace NotepadGuard;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly System.Timers.Timer _timer;
    private readonly BackupManager _backup;
    private readonly Config _config;
    private int _totalSaves;
    private int _cycles;

    private static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    public TrayApp()
    {
        _backup = new BackupManager();
        _config = Config.Load();

        _trayIcon = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = $"NotepadGuard v{Version} — every {_config.IntervalSeconds}s",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _timer = new System.Timers.Timer(_config.IntervalSeconds * 1000);
        _timer.Elapsed += (_, _) => DoCapture();
        _timer.AutoReset = true;
        _timer.Start();

        DoCapture();
        ShowBalloon($"NotepadGuard v{Version} — backup every {_config.IntervalSeconds}s");
    }

    private ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip();

        m.Items.Add("Open backup folder", null, (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _backup.BackupRoot,
                UseShellExecute = true
            }));

        m.Items.Add("Browse backups...", null, (_, _) =>
            new BackupViewerForm(_backup).Show());

        m.Items.Add(new ToolStripSeparator());

        // Interval submenu
        var intervalMenu = new ToolStripMenuItem("Backup interval");
        foreach (var secs in new[] { 30, 60, 120 })
        {
            var s = secs;
            var item = new ToolStripMenuItem($"{s} seconds")
            {
                Checked = _config.IntervalSeconds == s
            };
            item.Click += (_, _) => SetInterval(s, intervalMenu);
            intervalMenu.DropDownItems.Add(item);
        }
        m.Items.Add(intervalMenu);

        m.Items.Add(new ToolStripSeparator());

        m.Items.Add("Capture now", null, (_, _) =>
        {
            var n = DoCapture();
            ShowBalloon(n > 0 ? $"Saved {n} backup(s)." : "No changes detected.");
        });

        m.Items.Add("View diagnostic log", null, (_, _) =>
        {
            Logger.Log("--- log opened by user ---");
            Logger.OpenInNotepad();
        });

        m.Items.Add(new ToolStripSeparator());

        m.Items.Add("Exit", null, (_, _) =>
        {
            Logger.Log("SHUTDOWN: Exit clicked by user");
            DoCapture();
            _timer.Stop();
            _trayIcon.Visible = false;
            Application.Exit();
        });

        return m;
    }

    private void SetInterval(int seconds, ToolStripMenuItem menu)
    {
        _config.IntervalSeconds = seconds;
        _config.Save();
        _timer.Interval = seconds * 1000;
        Logger.Log($"interval changed to {seconds}s");
        _trayIcon.Text = $"NotepadGuard v{Version} — every {seconds}s";

        foreach (ToolStripMenuItem item in menu.DropDownItems)
            item.Checked = item.Text == $"{seconds} seconds";

        ShowBalloon($"Interval set to {seconds}s");
    }

    private int DoCapture()
    {
        try
        {
            int saved = 0;
            var snapshots = NotepadMonitor.CaptureAll();

            foreach (var snap in snapshots)
            {
                if (string.IsNullOrEmpty(snap.Content)) continue;
                var label = string.IsNullOrWhiteSpace(snap.WindowTitle) ? "Untitled" : snap.WindowTitle;
                if (_backup.SaveIfChanged(label, snap.Content))
                {
                    saved++;
                    Logger.Log($"saved backup: \"{label}\" ({snap.Content.Length} chars)");
                }
            }

            if (saved > 0)
            {
                _totalSaves += saved;
                _trayIcon.Text = $"NotepadGuard v{Version} — {_totalSaves} saved, every {_config.IntervalSeconds}s";
            }

            // Heartbeat: the last line of the log tells us exactly when the app was last alive.
            _cycles++;
            Logger.Log($"cycle #{_cycles}: {snapshots.Count} notepad(s), {saved} saved, {GC.GetTotalMemory(false) / 1024 / 1024} MB");

            return saved;
        }
        catch (Exception ex)
        {
            Logger.LogException("DoCapture", ex);
            return 0;
        }
    }

    private void ShowBalloon(string msg)
    {
        _trayIcon.BalloonTipTitle = "NotepadGuard";
        _trayIcon.BalloonTipText = msg;
        _trayIcon.ShowBalloonTip(2500);
    }

    private static Icon MakeIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var pageBrush = new SolidBrush(Color.White);
        using var pagePen = new Pen(Color.FromArgb(50, 80, 160), 1.5f);
        using var linePen = new Pen(Color.FromArgb(180, 200, 230));
        using var greenBrush = new SolidBrush(Color.FromArgb(40, 180, 80));
        using var checkPen = new Pen(Color.White, 2f);

        g.FillRectangle(pageBrush, 3, 1, 22, 27);
        g.DrawRectangle(pagePen, 3, 1, 22, 27);
        foreach (var y in new[] { 7, 12, 17, 22 })
            g.DrawLine(linePen, 7, y, 21, y);

        g.FillEllipse(greenBrush, 17, 17, 14, 14);
        g.DrawLine(checkPen, 21, 24, 24, 27);
        g.DrawLine(checkPen, 24, 27, 29, 20);

        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _trayIcon.Dispose(); }
        base.Dispose(disposing);
    }
}
