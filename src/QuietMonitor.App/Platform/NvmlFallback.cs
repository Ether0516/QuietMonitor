using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using QuietMonitor.Core;

namespace QuietMonitor.Platform;

// Optional driver DLL; no CUDA Toolkit, subprocess, or redistributed NVIDIA binary.
public sealed class NvmlFallback : IDisposable
{
    private IntPtr _library;
    private Shutdown? _shutdown;
    private Count? _count;
    private Handle? _handle;
    private Utilization? _utilization;
    private Name? _name, _uuid;
    public IReadOnlyList<GpuDevice> Devices { get; private set; } = Array.Empty<GpuDevice>();
    public NvmlFallback()
    {
        var candidates = new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvml.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvml.dll") };
        foreach (var path in candidates)
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out _library)) break;
        if (_library == IntPtr.Zero) return;
        try
        {
            if (Load<Init>("nvmlInit_v2")() != 0) { Dispose(); return; }
            _shutdown = Load<Shutdown>("nvmlShutdown");
            _count = Load<Count>("nvmlDeviceGetCount_v2");
            _handle = Load<Handle>("nvmlDeviceGetHandleByIndex_v2");
            _utilization = Load<Utilization>("nvmlDeviceGetUtilizationRates");
            _name = Load<Name>("nvmlDeviceGetName");
            _uuid = Load<Name>("nvmlDeviceGetUUID");
        }
        catch { Dispose(); }
    }
    public Dictionary<string, double> Read()
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var devices = new List<GpuDevice>();
        if (_count == null || _handle == null || _utilization == null || _count(out uint count) != 0) return values;
        for (uint i = 0; i < Math.Min(count, 32); i++)
        {
            if (_handle(i, out var device) != 0 || _utilization(device, out var util) != 0) continue;
            var uuid = new StringBuilder(128); var name = new StringBuilder(128);
            string id = _uuid != null && _uuid(device, uuid, 128) == 0 ? "nvml:" + uuid : "nvml:" + i;
            string label = _name != null && _name(device, name, 128) == 0 ? name.ToString() : "NVIDIA GPU " + i;
            devices.Add(new(id, label));
            values[id] = Math.Clamp(util.Gpu, 0, 100);
        }
        Devices = devices;
        return values;
    }
    private T Load<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, name));
    public void Dispose()
    {
        _shutdown?.Invoke(); _shutdown = null;
        _count = null; _handle = null; _utilization = null;
        if (_library != IntPtr.Zero) { NativeLibrary.Free(_library); _library = IntPtr.Zero; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Usage { public uint Gpu, Memory; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Init();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Shutdown();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Count(out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Handle(uint index, out IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Utilization(IntPtr device, out Usage usage);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int Name(IntPtr device, StringBuilder text, uint length);
}
