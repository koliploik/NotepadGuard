using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;

namespace NotepadGuard;

public class BackupViewerForm : Form
{
    private readonly BackupManager _backup;
    private readonly ListBox _labels;
    private readonly ListBox _versions;
    private readonly TextBox _preview;
    private List<BackupEntry>? _currentEntries;

    public BackupViewerForm(BackupManager backup)
    {
        _backup = backup;

        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        Text = $"NotepadGuard v{ver} — Backup Browser";
        Size = new Size(960, 620);
        MinimumSize = new Size(700, 400);
        StartPosition = FormStartPosition.CenterScreen;

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 340 };
        var leftSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 260
        };

        leftSplit.Panel1.Controls.Add(_labels = new ListBox { Dock = DockStyle.Fill });
        leftSplit.Panel1.Controls.Add(new Label { Text = "  Files", Dock = DockStyle.Top, Height = 22 });
        _labels.SelectedIndexChanged += (_, _) => LoadVersions();

        leftSplit.Panel2.Controls.Add(_versions = new ListBox { Dock = DockStyle.Fill });
        leftSplit.Panel2.Controls.Add(new Label { Text = "  Versions", Dock = DockStyle.Top, Height = 22 });
        _versions.SelectedIndexChanged += (_, _) => LoadPreview();

        split.Panel1.Controls.Add(leftSplit);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4, 6, 4, 4)
        };

        var btnOpen = new Button { Text = "Open in Notepad", AutoSize = true };
        btnOpen.Click += (_, _) => OpenInNotepad();

        var btnCopy = new Button { Text = "Copy to clipboard", AutoSize = true };
        btnCopy.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_preview.Text))
            {
                Clipboard.SetText(_preview.Text);
                MessageBox.Show("Copied!", "NotepadGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        var btnRefresh = new Button { Text = "Refresh", AutoSize = true };
        btnRefresh.Click += (_, _) => RefreshLabels();

        var btnOpenFolder = new Button { Text = "Open folder", AutoSize = true };
        btnOpenFolder.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _backup.BackupRoot,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        };

        btnPanel.Controls.AddRange([btnOpen, btnCopy, btnRefresh, btnOpenFolder]);

        _preview = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Both, WordWrap = false,
            Font = new Font("Consolas", 10f)
        };

        split.Panel2.Controls.Add(_preview);
        split.Panel2.Controls.Add(btnPanel);
        split.Panel2.Controls.Add(new Label { Text = "  Preview", Dock = DockStyle.Top, Height = 22 });

        Controls.Add(split);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        _labels.Items.Clear();
        _versions.Items.Clear();
        _preview.Text = "";
        _currentEntries = null;

        try
        {
            foreach (var l in _backup.GetLabels())
                _labels.Items.Add(l);
        }
        catch (Exception ex)
        {
            _preview.Text = $"Error loading labels: {ex.Message}";
        }
    }

    private void LoadVersions()
    {
        _versions.Items.Clear();
        _preview.Text = "";
        _currentEntries = null;
        if (_labels.SelectedItem is not string label) return;

        try
        {
            _currentEntries = _backup.GetBackups(label);
            foreach (var e in _currentEntries)
            {
                var kb = e.Size / 1024.0;
                _versions.Items.Add($"{e.Timestamp:yyyy-MM-dd HH:mm:ss}  ({kb:F1} KB)");
            }

            if (_currentEntries.Count == 0)
                _preview.Text = "(no backups found for this file)";
        }
        catch (Exception ex)
        {
            _preview.Text = $"Error loading versions: {ex.Message}";
        }
    }

    private void LoadPreview()
    {
        _preview.Text = "";
        if (_currentEntries == null || _versions.SelectedIndex < 0) return;
        if (_versions.SelectedIndex >= _currentEntries.Count) return;

        var entry = _currentEntries[_versions.SelectedIndex];
        try
        {
            if (!File.Exists(entry.FilePath))
            {
                _preview.Text = $"File not found: {entry.FilePath}";
                return;
            }
            _preview.Text = _backup.ReadBackup(entry.FilePath);
            if (string.IsNullOrEmpty(_preview.Text))
                _preview.Text = "(file is empty)";
        }
        catch (Exception ex)
        {
            _preview.Text = $"Error reading backup:\n{ex.Message}\n\nPath: {entry.FilePath}";
        }
    }

    private void OpenInNotepad()
    {
        if (string.IsNullOrEmpty(_preview.Text)) return;
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(),
                $"NotepadGuard_restored_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(tmp, _preview.Text);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{tmp}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening in Notepad:\n{ex.Message}", "Error");
        }
    }
}
