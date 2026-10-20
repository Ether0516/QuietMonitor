using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using QuietMonitor.Core;

namespace QuietMonitor;

public partial class SettingsWindow : Window
{
    private readonly App _app;
    private bool _ready;
    private sealed record IntervalChoice(int Ms, string Label);
    public SettingsWindow(App app)
    {
        _app = app; InitializeComponent();
        OpacitySlider.Value = app.Preferences.BackgroundOpacity * 100;
        FontSlider.Value = app.Preferences.FontSize;
        IntervalBox.ItemsSource = new[] { new IntervalChoice(500, "0.5 秒"), new IntervalChoice(1000, "1 秒"), new IntervalChoice(2000, "2 秒"), new IntervalChoice(5000, "5 秒") };
        IntervalBox.SelectedItem = IntervalBox.Items.Cast<IntervalChoice>().First(x => x.Ms == app.Preferences.IntervalMs);
        var devices = new List<GpuDevice> { new("auto", "自动 · 最繁忙显卡") };
        devices.AddRange(app.Sampler.Devices);
        if (app.Preferences.GpuId != "auto" && devices.All(x => x.Id != app.Preferences.GpuId))
            devices.Add(new(app.Preferences.GpuId, "已保存的显卡（当前不可用）"));
        GpuBox.ItemsSource = devices;
        GpuBox.SelectedItem = devices.First(x => x.Id == app.Preferences.GpuId);
        TopmostBox.IsChecked = app.Preferences.Topmost; LockBox.IsChecked = app.Preferences.Locked;
        HotkeyStatus.Text = app.Overlay.VisibilityHotkey && app.Overlay.RecoveryHotkey
            ? "快捷键已就绪" : "部分快捷键被其他程序占用；可随时使用托盘菜单。";
        _ready = true; Preview();
        OpacitySlider.ValueChanged += (_, _) => Preview(); FontSlider.ValueChanged += (_, _) => Preview();
        Closed += (_, _) => app.Overlay.ApplyPreferences();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        if (app.Sampler.Last != null) Update(app.Sampler.Last);
    }
    private void Preview()
    {
        if (!_ready) return;
        OpacityLabel.Text = $"{OpacitySlider.Value:0}%"; FontLabel.Text = $"{FontSlider.Value:0} px";
        _app.Overlay.PreviewAppearance(OpacitySlider.Value / 100, FontSlider.Value);
    }
    public void Update(MetricSnapshot value)
    {
        var devices = string.Join("\n", _app.Sampler.Devices.Select(x => x.Name));
        Diagnostics.Text = $"CPU {value.Cpu.Display} · {value.Cpu.Source}\n{value.Cpu.Detail}\n\nGPU {value.Gpu.Display} · {value.Gpu.Source}\n{value.Gpu.Detail}\n\n{devices}\n\n配置与日志：{_app.Store.DirectoryPath}\n\n总体 GPU = 每卡最忙引擎；自动模式取最忙显卡。覆盖计算、3D、复制及视频等实际引擎。";
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        var p = _app.Preferences;
        p.BackgroundOpacity = OpacitySlider.Value / 100; p.FontSize = FontSlider.Value;
        p.IntervalMs = ((IntervalChoice)IntervalBox.SelectedItem).Ms;
        p.GpuId = ((GpuDevice)GpuBox.SelectedItem).Id;
        p.Topmost = TopmostBox.IsChecked == true; p.Locked = LockBox.IsChecked == true;
        _app.SavePreferences(); _app.Overlay.ApplyPreferences(); _app.RefreshTray(); Close();
    }
    private void ResetPosition(object sender, RoutedEventArgs e) { _app.Overlay.ResetPosition(); _app.Overlay.Show(); }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
}
