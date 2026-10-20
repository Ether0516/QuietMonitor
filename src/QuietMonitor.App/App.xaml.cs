using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using QuietMonitor.Core;
using QuietMonitor.Platform;
using Forms = System.Windows.Forms;

namespace QuietMonitor;

public partial class App : System.Windows.Application
{
    public Preferences Preferences { get; private set; } = null!;
    public LocalStore Store { get; private set; } = null!;
    public MetricSampler Sampler { get; private set; } = null!;
    public OverlayWindow Overlay { get; private set; } = null!;
    public bool Exiting { get; private set; }
    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showSettingsEvent;
    private RegisteredWaitHandle? _showSettingsRegistration;
    private Forms.NotifyIcon? _tray;
    private Icon? _icon;
    private SettingsWindow? _settings;
    private Forms.ToolStripMenuItem? _lockItem;
    private long _lastUiTick;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var dataDirectory = Option(e.Args, "--data-dir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuietMonitor");
            Store = new LocalStore(Path.GetFullPath(dataDirectory)); Preferences = Store.Load();
            DispatcherUnhandledException += (_, args) => { Store.Log(args.Exception.ToString()); };
            Sampler = new MetricSampler(Preferences, Store);
            if (e.Args.Contains("--diagnose")) { await Diagnose(e.Args); return; }
            // Per-user and per-session; development data directories can have an independent instance.
            var identity = Environment.UserName + ":" + Store.DirectoryPath.ToLowerInvariant();
            var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)))[..20];
            _mutex = new Mutex(true, "Local\\QuietMonitor." + digest, out _ownsMutex);
            if (!_ownsMutex)
            {
                try { using var signal = EventWaitHandle.OpenExisting("Local\\QuietMonitor.Settings." + digest); signal.Set(); }
                catch (WaitHandleCannotBeOpenedException) { System.Windows.MessageBox.Show("Quiet Monitor 正在启动，请稍后从托盘打开。", "Quiet Monitor"); }
                Shutdown(); return;
            }
            _showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\QuietMonitor.Settings." + digest);
            _showSettingsRegistration = ThreadPool.RegisterWaitForSingleObject(_showSettingsEvent,
                (_, _) => { if (!Exiting) Dispatcher.BeginInvoke(new Action(OpenSettings)); }, null, Timeout.Infinite, false);
            Overlay = new OverlayWindow(this); MainWindow = Overlay;
            CreateTray(); Overlay.Show();
            Sampler.Updated += OnSample; Sampler.Start();
            var staleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            staleTimer.Tick += (_, _) =>
            {
                if (_lastUiTick != 0 && Environment.TickCount64 - _lastUiTick > Math.Max(8000, Preferences.IntervalMs * 3))
                {
                    var unavailable = new Reading(null, "采样", "数据已过期，等待新样本");
                    Overlay.Update(new(DateTimeOffset.Now, unavailable, unavailable, new Dictionary<string, double>()));
                }
            };
            staleTimer.Start();
            Exit += (_, _) => staleTimer.Stop();
            if (e.Args.Contains("--settings")) OpenSettings();
        }
        catch (Exception exception)
        {
            Store?.Log(exception.ToString());
            System.Windows.MessageBox.Show("启动失败：" + exception.Message, "Quiet Monitor"); Shutdown(1);
        }
    }
    private void OnSample(MetricSnapshot value)
    {
        if (Exiting || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (Exiting) return;
            _lastUiTick = Environment.TickCount64;
            Overlay.Update(value); _settings?.Update(value);
            if (_tray != null) _tray.Text = $"Quiet Monitor · CPU {value.Cpu.Display} · GPU {value.Gpu.Display}";
        }));
    }
    private void CreateTray()
    {
        _icon = CreateIcon();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏  (Ctrl+Alt+M)", null, (_, _) => { if (Overlay.IsVisible) Overlay.Hide(); else { Overlay.Show(); Overlay.ConstrainPosition(); } });
        menu.Items.Add("设置…", null, (_, _) => OpenSettings());
        _lockItem = new Forms.ToolStripMenuItem("锁定并鼠标穿透") { Checked = Preferences.Locked };
        _lockItem.Click += (_, _) => ToggleLock(); menu.Items.Add(_lockItem);
        menu.Items.Add("显示并解除穿透", null, (_, _) => { Overlay.Recover(); RefreshTray(); });
        menu.Items.Add("恢复默认位置", null, (_, _) => { Overlay.ResetPosition(); Overlay.Show(); });
        menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("退出", null, (_, _) => ExitApplication());
        _tray = new Forms.NotifyIcon { Icon = _icon, Text = "Quiet Monitor · CPU / GPU", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => OpenSettings();
    }
    public void OpenSettings()
    {
        if (_settings != null) { _settings.Activate(); return; }
        _settings = new SettingsWindow(this);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show(); _settings.Activate();
    }
    public void ToggleLock()
    {
        Preferences.Locked = !Preferences.Locked; SavePreferences(); Overlay.ApplyPreferences(); RefreshTray();
        if (Preferences.Locked && _tray != null)
            _tray.ShowBalloonTip(3500, "已启用鼠标穿透", "从托盘菜单解除，或按 Ctrl + Alt + Shift + M。", Forms.ToolTipIcon.Info);
    }
    public void SavePreferences() => Store.Save(Preferences);
    public void RefreshTray() { if (_lockItem != null) _lockItem.Checked = Preferences.Locked; }
    public void ExitApplication()
    {
        if (Exiting) return;
        Exiting = true; _settings?.Close(); Overlay.Close(); Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Exiting = true;
        if (Sampler != null) { Sampler.Updated -= OnSample; Sampler.Dispose(); }
        if (_tray != null) { _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); }
        _icon?.Dispose();
        _showSettingsRegistration?.Unregister(null); _showSettingsEvent?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex(); _mutex?.Dispose();
        base.OnExit(e);
    }
    private async Task Diagnose(string[] args)
    {
        var output = Option(args, "--output") ?? Path.Combine(Store.DirectoryPath, "diagnostics.json");
        int count = int.TryParse(Option(args, "--samples"), out int requested) ? Math.Clamp(requested, 2, 60) : 5;
        var samples = new List<MetricSnapshot>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Capture(MetricSnapshot sample) { lock (samples) { samples.Add(sample); if (samples.Count >= count) completed.TrySetResult(); } }
        Sampler.Updated += Capture; Sampler.Start();
        var finished = await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(count * 6 + 10)));
        Sampler.Updated -= Capture; Sampler.Dispose();
        MetricSnapshot[] copy; lock (samples) copy = samples.ToArray();
        var report = new { Version = "0.1.0", OS = Environment.OSVersion.ToString(), Processors = Environment.ProcessorCount,
            Devices = Sampler.Devices, Completed = finished == completed.Task, Samples = copy };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        // OnExit also calls Dispose; avoid double disposal after the diagnostic run.
        Sampler = null!; Shutdown(finished == completed.Task ? 0 : 2);
    }
    private static string? Option(string[] args, string name)
    { int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.FromArgb(23, 31, 45));
        using var brush = new SolidBrush(System.Drawing.Color.FromArgb(151, 188, 239));
        graphics.FillRectangle(brush, 7, 15, 6, 10); graphics.FillRectangle(brush, 19, 7, 6, 18);
        IntPtr handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
