using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;

namespace NotepadGuard;

public class BackupViewerForm : Form
{
    private readonly BackupManager _backup;
    private readonly ListBox _labels;
    private readonly ListBox _versions;
    private readonly RichTextBox _preview;
    private List<BackupEntry>? _currentEntries;
    private readonly Timer _refreshTimer;

    public BackupViewerForm(BackupManager backup)
    {
        _backup = backup;

        var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        Text = $"NotepadGuard v{ver} — Backup Browser";
        Size = new Size(960, 620);
        MinimumSize = new Size(700, 400);
        StartPosition = FormStartPosition.CenterScreen;

        // --- Main horizontal split ---
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 300,
            BorderStyle = BorderStyle.FixedSingle
        };

        // --- Left: vertical split (files + versions) ---
        var leftSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 240
        };

        // Files panel
        var filesLabel = new Label
        {
            Text = "  Files",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = SystemColors.ControlLight
        };
        _labels = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _labels.SelectedIndexChanged += Labels_SelectedIndexChanged;
        leftSplit.Panel1.Controls.Add(_labels);
        leftSplit.Panel1.Controls.Add(filesLabel);

        // Versions panel
        var versionsLabel = new Label
        {
            Text = "  Versions",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = SystemColors.ControlLight
        };
        _versions = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _versions.SelectedIndexChanged += Versions_SelectedIndexChanged;
        leftSplit.Panel2.Controls.Add(_versions);
        leftSplit.Panel2.Controls.Add(versionsLabel);

        split.Panel1.Controls.Add(leftSplit);

        // --- Right: preview + buttons ---
        var previewLabel = new Label
        {
            Text = "  Preview",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = SystemColors.ControlLight
        };

        _preview = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Consolas", 10f),
            BackColor = SystemColors.Window,
            Text = "Select a file on the left, then a version below it."
        };

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(4, 4, 4, 4)
        };

        var btnOpen = new Button { Text = "Open in Notepad", AutoSize = true, Margin = new Padding(2) };
        btnOpen.Click += BtnOpen_Click;

        var btnCopy = new Button { Text = "Copy to clipboard", AutoSize = true, Margin = new Padding(2) };
        btnCopy.Click += BtnCopy_Click;

        var btnRefresh = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(2) };
        btnRefresh.Click += (_, _) => RefreshLabels();

        var btnFolder = new Button { Text = "Open folder", AutoSize = true, Margin = new Padding(2) };
        btnFolder.Click += BtnFolder_Click;

        btnPanel.Controls.AddRange([btnOpen, btnCopy, btnRefresh, btnFolder]);

        split.Panel2.Controls.Add(_preview);
        split.Panel2.Controls.Add(btnPanel);
        split.Panel2.Controls.Add(previewLabel);

        Controls.Add(split);
        RefreshLabels();

        _refreshTimer = new Timer { Interval = 3000 };
        _refreshTimer.Tick += (_, _) => AutoRefresh();
        _refreshTimer.Start();

        FormClosed += (_, _) => { _refreshTimer.Stop(); _refreshTimer.Dispose(); };
    }

    private void AutoRefresh()
    {
        try
        {
            // Refresh file list if new files appeared
            var currentLabels = _backup.GetLabels();
            if (currentLabels.Count != _labels.Items.Count)
            {
                var selectedLabel = _labels.SelectedItem as string;
                _labels.Items.Clear();
                foreach (var l in currentLabels)
                    _labels.Items.Add(l);
                if (selectedLabel != null)
                    _labels.SelectedItem = selectedLabel;
                return;
            }

            // Refresh versions list if a file is selected
            if (_labels.SelectedItem is not string label) return;
            var freshEntries = _backup.GetBackups(label);
            if (_currentEntries != null && freshEntries.Count != _currentEntries.Count)
            {
                var selectedIdx = _versions.SelectedIndex;
                _currentEntries = freshEntries;
                _versions.Items.Clear();
                foreach (var entry in _currentEntries)
                {
                    var kb = entry.Size / 1024.0;
                    _versions.Items.Add($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}  ({kb:F1} KB)");
                }
                if (selectedIdx >= 0 && selectedIdx < _versions.Items.Count)
                    _versions.SelectedIndex = selectedIdx;
                else if (_versions.Items.Count > 0)
                    _versions.SelectedIndex = 0;
            }
        }
        catch { }
    }

    private void RefreshLabels()
    {
        _labels.Items.Clear();
        _versions.Items.Clear();
        _preview.Text = "";
        _currentEntries = null;

        try
        {
            var labels = _backup.GetLabels();
            foreach (var l in labels)
                _labels.Items.Add(l);

            if (labels.Count == 0)
                _preview.Text = "No backups found yet.\nOpen Notepad with some text and wait 60 seconds.";
            else if (labels.Count == 1)
                _labels.SelectedIndex = 0;
            else
                _preview.Text = $"{labels.Count} files backed up. Select one on the left.";
        }
        catch (Exception ex)
        {
            _preview.Text = $"Error loading labels:\n{ex.Message}\n\nBackup root: {_backup.BackupRoot}";
        }
    }

    private void Labels_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _versions.Items.Clear();
        _preview.Text = "";
        _currentEntries = null;
        if (_labels.SelectedItem is not string label) return;

        try
        {
            _currentEntries = _backup.GetBackups(label);
            foreach (var entry in _currentEntries)
            {
                var kb = entry.Size / 1024.0;
                _versions.Items.Add($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}  ({kb:F1} KB)");
            }

            if (_currentEntries.Count == 0)
            {
                _preview.Text = "(no backup versions found)";
            }
            else
            {
                _versions.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _preview.Text = $"Error loading versions for '{label}':\n{ex.Message}";
        }
    }

    private void Versions_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _preview.Text = "";
        if (_currentEntries == null) return;
        if (_versions.SelectedIndex < 0 || _versions.SelectedIndex >= _currentEntries.Count) return;

        var entry = _currentEntries[_versions.SelectedIndex];
        try
        {
            if (!File.Exists(entry.FilePath))
            {
                _preview.Text = $"File not found:\n{entry.FilePath}";
                return;
            }
            var content = _backup.ReadBackup(entry.FilePath);
            _preview.Text = string.IsNullOrEmpty(content) ? "(file is empty)" : content;
        }
        catch (Exception ex)
        {
            _preview.Text = $"Error reading backup:\n{ex.Message}\n\nPath: {entry.FilePath}";
        }
    }

    private void BtnOpen_Click(object? sender, EventArgs e)
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
            MessageBox.Show($"Cannot open in Notepad:\n{ex.Message}", "Error");
        }
    }

    private void BtnCopy_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_preview.Text))
        {
            Clipboard.SetText(_preview.Text);
            MessageBox.Show("Copied!", "NotepadGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BtnFolder_Click(object? sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _backup.BackupRoot,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot open folder:\n{ex.Message}\n\nPath: {_backup.BackupRoot}", "Error");
        }
    }
}
