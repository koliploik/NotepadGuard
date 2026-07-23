using System;
using System.IO;
using System.Text.Json;

namespace NotepadGuard;

public class Config
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "NotepadGuard", "config.json");

    public int IntervalSeconds { get; set; } = 60;

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath)) ?? new Config();
        }
        catch { }
        return new Config();
    }
}
