using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;

namespace NotepadGuard;

public record NotepadSnapshot(string WindowTitle, string Content, int ProcessId, DateTime Timestamp);

public static class NotepadMonitor
{
    public static List<NotepadSnapshot> CaptureAll()
    {
        var snapshots = new List<NotepadSnapshot>();

        foreach (var proc in Process.GetProcessesByName("Notepad"))
        {
            try
            {
                if (proc.MainWindowHandle == IntPtr.Zero) continue;

                var element = AutomationElement.FromHandle(proc.MainWindowHandle);
                var content = GetTextContent(element);

                if (content != null)
                {
                    snapshots.Add(new NotepadSnapshot(
                        proc.MainWindowTitle,
                        content,
                        proc.Id,
                        DateTime.Now));
                }
            }
            catch (Exception ex)
            {
                // Window may have closed between enumeration and access
                Logger.Log($"could not read notepad pid {proc.Id}: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                proc.Dispose();
            }
        }

        return snapshots;
    }

    private static string? GetTextContent(AutomationElement window)
    {
        // Windows 11 Notepad: Document control type (RichEditD2DPT)
        var doc = window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
        if (doc != null)
            return ExtractText(doc);

        // Classic Notepad: Edit control type
        var edit = window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        if (edit != null)
            return ExtractText(edit);

        // Fallback: walk the tree looking for any text-bearing element
        return WalkForText(TreeWalker.ContentViewWalker, window, 0);
    }

    private static string? ExtractText(AutomationElement el)
    {
        if (el.TryGetCurrentPattern(TextPattern.Pattern, out var tp))
            return ((TextPattern)tp).DocumentRange.GetText(-1);

        if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vp))
            return ((ValuePattern)vp).Current.Value;

        return null;
    }

    private static string? WalkForText(TreeWalker walker, AutomationElement el, int depth)
    {
        if (depth > 10) return null;

        var text = ExtractText(el);
        if (text != null) return text;

        var child = walker.GetFirstChild(el);
        while (child != null)
        {
            text = WalkForText(walker, child, depth + 1);
            if (text != null) return text;
            child = walker.GetNextSibling(child);
        }

        return null;
    }
}
