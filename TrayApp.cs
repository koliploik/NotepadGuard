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
    private int _totalSaves;

    private static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    public TrayApp()
    {
        _backup = new BackupManager();

        _trayIcon = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = $"NotepadGuard v{Version} — monitoring",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _timer = new System.Timers.Timer(60_000);
        _timer.Elapsed += (_, _) => DoCapture();
        _timer.AutoReset = true;
        _timer.Start();

        DoCapture();
        ShowBalloon($"NotepadGuard v{Version} active. Backup every 60 s.");
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

        m.Items.Add("Capture now", null, (_, _) =>
        {
            var n = DoCapture();
            ShowBalloon(n > 0 ? $"Saved {n} backup(s)." : "No changes detected.");
        });

        m.Items.Add(new ToolStripSeparator());

        m.Items.Add("Exit", null, (_, _) =>
        {
            DoCapture();
            _timer.Stop();
            _trayIcon.Visible = false;
            Application.Exit();
        });

        return m;
    }

    private int DoCapture()
    {
        try
        {
            int saved = 0;
            foreach (var snap in NotepadMonitor.CaptureAll())
            {
                if (string.IsNullOrEmpty(snap.Content)) continue;
                var label = string.IsNullOrWhiteSpace(snap.WindowTitle) ? "Untitled" : snap.WindowTitle;
                if (_backup.SaveIfChanged(label, snap.Content))
                    saved++;
            }
            if (saved > 0)
            {
                _totalSaves += saved;
                _trayIcon.Text = $"NotepadGuard — {_totalSaves} backup(s)";
            }
            return saved;
        }
        catch { return 0; }
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
        var icon = Icon.FromHandle(handle);
        return icon;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _trayIcon.Dispose(); }
        base.Dispose(disposing);
    }
}
