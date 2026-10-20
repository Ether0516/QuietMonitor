using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using QuietMonitor.Core;

namespace QuietMonitor.Platform;

public static class GpuDevices
{
    public static IReadOnlyList<GpuDevice> Enumerate()
    {
        var devices = new List<GpuDevice>();
        var iid = new Guid("770AAE78-F26F-4DBA-A829-253C83D1B387"); // IDXGIFactory1
        if (CreateDXGIFactory1(ref iid, out var factory) < 0) return devices;
        try
        {
            var enumerate = Method<EnumAdapters1>(factory, 12);
            for (uint i = 0; i < 32; i++)
            {
                if (enumerate(factory, i, out var adapter) < 0) break;
                try
                {
                    if (Method<GetDesc1>(adapter, 10)(adapter, out var desc) >= 0 && (desc.Flags & 2) == 0)
                        devices.Add(new GpuDevice(GpuMath.Luid(desc.LowPart, desc.HighPart), $"GPU {i} · {desc.Description.TrimEnd('\0')}"));
                }
                finally { Marshal.Release(adapter); }
            }
        }
        finally { Marshal.Release(factory); }
        return devices;
    }
    private static T Method<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1(IntPtr self, uint index, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1(IntPtr self, out AdapterDesc desc);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDesc
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId, DeviceId, SubSysId, Revision;
        public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public uint LowPart;
        public int HighPart;
        public uint Flags;
    }
    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(ref Guid iid, out IntPtr factory);
}
