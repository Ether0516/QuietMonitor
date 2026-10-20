using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using QuietMonitor.Core;
using Forms = System.Windows.Forms;

namespace QuietMonitor;

public partial class OverlayWindow : Window
{
    private readonly App _app;
    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly DispatcherTimer _saveTimer;
    private bool _positioned;
    public bool VisibilityHotkey { get; private set; }
    public bool RecoveryHotkey { get; private set; }
    public OverlayWindow(App app)
    {
        _app = app; InitializeComponent();
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SavePosition(); };
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            _source = HwndSource.FromHwnd(_hwnd); _source.AddHook(WindowProc);
            VisibilityHotkey = RegisterHotKey(_hwnd, 1, 0x4000 | 0x2 | 0x1, 0x4D); // Ctrl+Alt+M
            RecoveryHotkey = RegisterHotKey(_hwnd, 2, 0x4000 | 0x2 | 0x1 | 0x4, 0x4D);
            ApplyPreferences();
        };
        Loaded += (_, _) => { RestorePosition(); _positioned = true; };
        LocationChanged += (_, _) => { if (_positioned) { _saveTimer.Stop(); _saveTimer.Start(); } };
        SizeChanged += (_, _) => { if (_positioned) ConstrainPosition(); };
        MouseLeftButtonDown += OnDrag;
        MouseRightButtonUp += (_, _) => { if (!_app.Preferences.Locked) BuildMenu().IsOpen = true; };
        MouseDoubleClick += (_, e) => { if (e.ChangedButton == MouseButton.Left && !_app.Preferences.Locked) _app.OpenSettings(); };
        Closing += (_, e) => { if (!_app.Exiting) { e.Cancel = true; Hide(); } };
        Closed += (_, _) =>
        {
            _saveTimer.Stop(); SavePosition();
            UnregisterHotKey(_hwnd, 1); UnregisterHotKey(_hwnd, 2); _source?.RemoveHook(WindowProc);
        };
    }
    public void ApplyPreferences()
    {
        var p = _app.Preferences;
        Topmost = p.Topmost;
        PreviewAppearance(p.BackgroundOpacity, p.FontSize);
        Cursor = p.Locked ? Cursors.Arrow : Cursors.SizeAll;
        if (_hwnd != IntPtr.Zero)
        {
            long style = GetWindowLongPtrW(_hwnd, -20).ToInt64() | 0x80 | 0x08000000; // tool/noactivate
            if (p.Locked) style |= 0x20; else style &= ~0x20;
            SetWindowLongPtrW(_hwnd, -20, new IntPtr(style));
            SetWindowPos(_hwnd, p.Topmost ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x1 | 0x2 | 0x10 | 0x20);
        }
    }
    public void PreviewAppearance(double opacity, double fontSize)
    {
        Panel.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(opacity * 255), 20, 25, 35));
        FontSize = fontSize;
        CpuValue.Width = GpuValue.Width = Math.Ceiling(fontSize * 3.1);
    }
    public void Update(MetricSnapshot snapshot)
    {
        CpuValue.Text = snapshot.Cpu.Display; GpuValue.Text = snapshot.Gpu.Display;
        ToolTip = $"CPU · {snapshot.Cpu.Source}\n{snapshot.Cpu.Detail}\n\nGPU · {snapshot.Gpu.Source}\n{snapshot.Gpu.Detail}\n\n左键拖动 · 右键菜单 · 双击设置\nCtrl + Alt + M：显示/隐藏\nCtrl + Alt + Shift + M：显示并解除穿透";
    }
    public void Recover()
    {
        _app.Preferences.Locked = false; _app.SavePreferences(); ApplyPreferences(); Show(); ConstrainPosition(); _app.RefreshTray();
    }
    public void ResetPosition()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 28; Top = area.Top + 28;
        ConstrainPosition(); SavePosition();
    }
    private void RestorePosition()
    {
        if (_app.Preferences.Left.HasValue && _app.Preferences.Top.HasValue)
        { Left = _app.Preferences.Left.Value; Top = _app.Preferences.Top.Value; ConstrainPosition(); }
        else ResetPosition();
    }
    public void ConstrainPosition()
    {
        if (_hwnd == IntPtr.Zero || !GetWindowRect(_hwnd, out var rect)) return;
        var screen = Forms.Screen.FromHandle(_hwnd).WorkingArea;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var bounds = new Rect(transform.Transform(new Point(screen.Left, screen.Top)), transform.Transform(new Point(screen.Right, screen.Bottom)));
        Left = Math.Clamp(Left, bounds.Left, Math.Max(bounds.Left, bounds.Right - ActualWidth));
        Top = Math.Clamp(Top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - ActualHeight));
    }
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (_app.Preferences.Locked || e.ClickCount != 1) return;
        try { DragMove(); SnapToEdge(); SavePosition(); } catch (InvalidOperationException) { }
    }
    private void SnapToEdge()
    {
        ConstrainPosition();
        var area = Forms.Screen.FromHandle(_hwnd).WorkingArea;
        var t = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = t.Transform(new Point(area.Left, area.Top)); var bottomRight = t.Transform(new Point(area.Right, area.Bottom));
        if (Math.Abs(Left - topLeft.X) < 16) Left = topLeft.X;
        if (Math.Abs(Top - topLeft.Y) < 16) Top = topLeft.Y;
        if (Math.Abs(Left + ActualWidth - bottomRight.X) < 16) Left = bottomRight.X - ActualWidth;
        if (Math.Abs(Top + ActualHeight - bottomRight.Y) < 16) Top = bottomRight.Y - ActualHeight;
    }
    private void SavePosition()
    { if (!_positioned) return; _app.Preferences.Left = Left; _app.Preferences.Top = Top; _app.SavePreferences(); }
    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        void Add(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Add("设置…", _app.OpenSettings); Add("锁定并鼠标穿透", _app.ToggleLock);
        Add("恢复默认位置", ResetPosition); Add("隐藏", Hide); menu.Items.Add(new Separator()); Add("退出", _app.ExitApplication);
        menu.PlacementTarget = this; return menu;
    }
    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (message == 0x0312)
        {
            if (wParam.ToInt32() == 1) { if (IsVisible) Hide(); else { Show(); ConstrainPosition(); } }
            if (wParam.ToInt32() == 2) Recover(); handled = true;
        }
        if (message is 0x007E or 0x02E0 or 0x001A)
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ConstrainPosition));
        if (message == 0x218 && (wParam.ToInt32() == 7 || wParam.ToInt32() == 18))
        {
            _app.Sampler.ResetBaselines();
            Dispatcher.BeginInvoke(new Action(ConstrainPosition));
        }
        return IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
