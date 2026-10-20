using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using QuietMonitor.Core;

namespace QuietMonitor.Platform;

public sealed class MetricSampler : IDisposable
{
    private readonly Preferences _preferences;
    private readonly LocalStore _store;
    private readonly CancellationTokenSource _stop = new();
    private PdhQuery? _gpu, _cpu;
    private NvmlFallback? _nvml;
    private Task? _loop;
    private long _nextGpuAttempt, _nextCpuAttempt, _nextDevices;
    private int _gpuFailures, _cpuFailures;
    private ulong _idle, _kernel, _user;
    private bool _cpuPrimed;
    private int _disposed;
    private int _resetRequested;
    private string _cpuPath = "Windows CPU time";
    private string? _lastFailure;
    public IReadOnlyList<GpuDevice> Devices { get; private set; } = Array.Empty<GpuDevice>();
    private IReadOnlyList<GpuDevice> _windowsDevices = Array.Empty<GpuDevice>();
    public MetricSnapshot? Last { get; private set; }
    public event Action<MetricSnapshot>? Updated;
    public MetricSampler(Preferences preferences, LocalStore store) { _preferences = preferences; _store = store; }
    public void Start() { if (_loop == null) _loop = Task.Run(Run); }
    public void ResetBaselines() => Interlocked.Exchange(ref _resetRequested, 1);
    private async Task Run()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var started = Environment.TickCount64;
                try
                {
                    var value = Sample(); Last = value; Updated?.Invoke(value);
                }
                catch (Exception e) { _store.Log("Sampling: " + e.Message); }
                int delay = Math.Max(100, _preferences.IntervalMs - (int)Math.Min(10000, Environment.TickCount64 - started));
                await Task.Delay(delay, _stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally { _cpu?.Dispose(); _gpu?.Dispose(); _nvml?.Dispose(); }
    }
    private MetricSnapshot Sample()
    {
        if (Interlocked.Exchange(ref _resetRequested, 0) != 0)
        {
            _cpu?.Dispose(); _cpu = null; _gpu?.Dispose(); _gpu = null;
            _cpuPrimed = false; _nextCpuAttempt = _nextGpuAttempt = _nextDevices = 0;
        }
        var now = Environment.TickCount64;
        if (now >= _nextDevices)
        {
            try { _windowsDevices = GpuDevices.Enumerate(); if (Devices.Count == 0) Devices = _windowsDevices; } catch (Exception e) { _store.Log("DXGI: " + e.Message); }
            _nextDevices = now + 30000;
        }
        var cpu = ReadCpu(now);
        var (gpu, perDevice) = ReadGpu(now);
        return new MetricSnapshot(DateTimeOffset.Now, cpu, gpu, perDevice);
    }
    private Reading ReadCpu(long now)
    {
        if (_cpu == null && now >= _nextCpuAttempt)
        {
            foreach (string path in new[] { @"\Processor Information(_Total)\% Processor Utility", @"\Processor(_Total)\% Processor Time" })
                try { _cpu = new PdhQuery(path); _cpuPath = path; _cpuFailures = 0; break; } catch { }
            _nextCpuAttempt = now + 30000;
            if (_cpu != null) return new(null, "Windows PDH", "正在建立 CPU 采样基线");
        }
        if (_cpu != null)
        {
            try
            {
                var data = _cpu.Read();
                if (data.Count > 0) { _cpuFailures = 0; return new(Math.Clamp(data.Values.Max(), 0, 100), "Windows PDH", _cpuPath); }
            }
            catch
            {
                if (++_cpuFailures >= 3) { _cpu.Dispose(); _cpu = null; _nextCpuAttempt = now + 15000; }
            }
        }
        if (GetActiveProcessorCount(ushort.MaxValue) > 64)
            return new(null, "Windows", "性能计数器不可用；多处理器组不使用单组回退值");
        if (!GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
            return new(null, "Windows CPU time", "无法读取处理器时间");
        var percent = _cpuPrimed ? CpuMath.Busy(_idle, _kernel, _user, idle, kernel, user) : null;
        _idle = idle; _kernel = kernel; _user = user; _cpuPrimed = true;
        return new(percent, "Windows CPU time", "处理器忙碌时间（回退来源）");
    }
    private (Reading, Dictionary<string, double>) ReadGpu(long now)
    {
        string? error = null;
        if (_gpu == null && now >= _nextGpuAttempt)
        {
            try
            {
                _gpu = new PdhQuery(@"\GPU Engine(*)\Utilization Percentage"); _gpuFailures = 0;
                return (new(null, "Windows GPU Engine", "正在建立 GPU 采样基线"), new());
            }
            catch (Exception e) { error = e.Message; _nextGpuAttempt = now + 15000; }
        }
        if (_gpu != null)
        {
            try
            {
                var raw = _gpu.Read();
                var loads = raw.Select(x => GpuMath.Parse(x.Key, x.Value)).Where(x => x != null).Cast<GpuLoad>().ToArray();
                if (raw.Count > 0 && loads.Length == 0) throw new InvalidOperationException("无法识别 GPU 引擎实例格式");
                var data = GpuMath.Aggregate(loads);
                // A successful query with no active context can be idle; a failed query is never zero.
                foreach (var device in _windowsDevices) data.TryAdd(device.Id, 0);
                if (data.Count == 0) throw new InvalidOperationException("未发现可用 GPU 设备/引擎");
                _gpuFailures = 0;
                Devices = _windowsDevices;
                return SelectGpu(data, "Windows GPU Engine", $"全部引擎 · {loads.Length} 个进程引擎实例 · 每卡取最忙引擎");
            }
            catch (Exception e)
            {
                error = e.Message;
                if (++_gpuFailures >= 3) { _gpu.Dispose(); _gpu = null; _nextGpuAttempt = now + 15000; }
            }
        }
        if (error != null && _lastFailure != error) { _store.Log("GPU: " + error); _lastFailure = error; }
        _nvml ??= new NvmlFallback();
        var fallback = _nvml.Read();
        if (fallback.Count > 0) { Devices = _nvml.Devices; return SelectGpu(fallback, "NVIDIA NVML（回退）", "整卡 kernel 时间活动；与 Windows 引擎口径不同"); }
        return (new(null, "GPU", error ?? "Windows 计数器暂不可用，正在重试；未发现可用厂商回退"), new());
    }
    private (Reading, Dictionary<string, double>) SelectGpu(Dictionary<string, double> data, string source, string detail)
    {
        if (_preferences.GpuId != "auto")
        {
            if (!data.TryGetValue(_preferences.GpuId, out var chosen))
                return (new(null, source, "所选显卡当前不可用；可在设置中选择自动"), data);
            var device = Devices.FirstOrDefault(x => x.Id.Equals(_preferences.GpuId, StringComparison.OrdinalIgnoreCase));
            return (new(chosen, source, $"{device?.Name ?? _preferences.GpuId}\n{detail}"), data);
        }
        var busiest = data.MaxBy(x => x.Value);
        var name = Devices.FirstOrDefault(x => x.Id.Equals(busiest.Key, StringComparison.OrdinalIgnoreCase))?.Name ?? busiest.Key;
        return (new(busiest.Value, source, $"自动 · {name}\n{detail}"), data);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        // Native queries belong to the worker; only dispose them after sampling has stopped.
        if (_loop != null) { try { _loop.GetAwaiter().GetResult(); } catch (OperationCanceledException) { } }
        _stop.Dispose();
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] private static extern uint GetActiveProcessorCount(ushort group);
}
