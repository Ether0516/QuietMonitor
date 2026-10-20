using System;
using System.IO;
using System.Text.Json;

namespace QuietMonitor;

public sealed class Preferences
{
    public int SchemaVersion { get; set; } = 1;
    public double BackgroundOpacity { get; set; } = 0.70;
    public double FontSize { get; set; } = 17;
    public int IntervalMs { get; set; } = 1000;
    public bool Locked { get; set; }
    public bool Topmost { get; set; } = true;
    public string GpuId { get; set; } = "auto";
    public double? Left { get; set; }
    public double? Top { get; set; }
    public void Normalize()
    {
        BackgroundOpacity = double.IsFinite(BackgroundOpacity) ? Math.Clamp(BackgroundOpacity, .2, 1) : .7;
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, 12, 28) : 17;
        if (IntervalMs is not (500 or 1000 or 2000 or 5000)) IntervalMs = 1000;
        if (Left.HasValue && !double.IsFinite(Left.Value)) Left = null;
        if (Top.HasValue && !double.IsFinite(Top.Value)) Top = null;
        GpuId = string.IsNullOrWhiteSpace(GpuId) ? "auto" : GpuId;
    }
}

public sealed class LocalStore
{
    private readonly object _gate = new();
    public string DirectoryPath { get; }
    public string PreferencesPath => Path.Combine(DirectoryPath, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public LocalStore(string directory) { DirectoryPath = directory; Directory.CreateDirectory(directory); }
    public Preferences Load()
    {
        try
        {
            var value = File.Exists(PreferencesPath)
                ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencesPath)) ?? new() : new();
            value.Normalize();
            return value;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        { Log("Load settings: " + e.Message); return new(); }
    }
    public void Save(Preferences preferences)
    {
        try
        {
            lock (_gate)
            {
                preferences.Normalize();
                var tmp = PreferencesPath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(preferences, JsonOptions));
                File.Move(tmp, PreferencesPath, true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Log("Save settings: " + e.Message); }
    }
    public void Log(string message)
    {
        try
        {
            lock (_gate)
            {
                var path = Path.Combine(DirectoryPath, "monitor.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                    File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
