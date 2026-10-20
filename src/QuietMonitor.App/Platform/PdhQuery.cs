using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace QuietMonitor.Platform;

// A language-neutral wildcard query. Native array reading preserves engine instance identity.
public sealed class PdhQuery : IDisposable
{
    private IntPtr _query;
    private IntPtr _counter;
    private const uint MoreData = 0x800007D2;
    private const uint NoData = 0x800007D5;
    public PdhQuery(string englishPath)
    {
        Check(PdhOpenQueryW(null, UIntPtr.Zero, out _query), "open");
        try
        {
            Check(PdhAddEnglishCounterW(_query, englishPath, UIntPtr.Zero, out _counter), "add");
            // Convert English object/counter names once, then use a localized wildcard counter
            // with PdhGetFormattedCounterArray (which resolves dynamic instances each sample).
            uint bytes = 0;
            uint status = PdhGetCounterInfoW(_counter, false, ref bytes, IntPtr.Zero);
            if (status != MoreData || bytes == 0) Check(status, "counter info size");
            var buffer = Marshal.AllocHGlobal(checked((int)bytes));
            try
            {
                Check(PdhGetCounterInfoW(_counter, false, ref bytes, buffer), "counter info");
                var header = Marshal.PtrToStructure<CounterInfoHeader>(buffer);
                var localizedPath = Marshal.PtrToStringUni(header.FullPath);
                if (!string.IsNullOrWhiteSpace(localizedPath))
                {
                    Check(PdhAddCounterW(_query, localizedPath, UIntPtr.Zero, out var localized), "localized add");
                    PdhRemoveCounter(_counter);
                    _counter = localized;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
            var initial = PdhCollectQueryData(_query);
            if (initial != NoData) Check(initial, "prime");
        }
        catch { Dispose(); throw; }
    }
    public Dictionary<string, double> Read()
    {
        Check(PdhCollectQueryData(_query), "collect");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            uint bytes = 0;
            uint status = PdhGetFormattedCounterArrayW(_counter, 0x200 | 0x8000, ref bytes, out var count, IntPtr.Zero);
            if (status == 0 && count == 0) return new(StringComparer.OrdinalIgnoreCase);
            if (status != MoreData || bytes == 0) Check(status, "array size");
            var buffer = Marshal.AllocHGlobal(checked((int)bytes));
            try
            {
                status = PdhGetFormattedCounterArrayW(_counter, 0x200 | 0x8000, ref bytes, out count, buffer);
                if (status == MoreData) continue;
                Check(status, "array");
                var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                int stride = Marshal.SizeOf<FormattedItem>();
                for (int i = 0; i < count; i++)
                {
                    var item = Marshal.PtrToStructure<FormattedItem>(IntPtr.Add(buffer, i * stride));
                    if (item.Value.Status > 1 || !double.IsFinite(item.Value.Value)) continue;
                    var name = Marshal.PtrToStringUni(item.Name);
                    if (name != null) result[name] = item.Value.Value;
                }
                if (count > 0 && result.Count == 0) throw new InvalidOperationException("PDH sample warming up / invalid instances");
                return result;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new InvalidOperationException("PDH instance set changed during read");
    }
    private static void Check(uint status, string operation)
    { if (status != 0) throw new InvalidOperationException($"PDH {operation}: 0x{status:X8}"); }
    public void Dispose() { if (_query != IntPtr.Zero) { PdhCloseQuery(_query); _query = IntPtr.Zero; } }

    [StructLayout(LayoutKind.Sequential)]
    private struct CounterInfoHeader
    {
        public uint Length, Type, Version, Status;
        public int Scale, DefaultScale;
        public UIntPtr UserData, QueryUserData;
        public IntPtr FullPath;
    }
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct FormattedValue
    { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double Value; }
    [StructLayout(LayoutKind.Sequential)]
    private struct FormattedItem { public IntPtr Name; public FormattedValue Value; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhOpenQueryW(string? source, UIntPtr data, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, UIntPtr data, out IntPtr counter);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhAddCounterW(IntPtr query, string path, UIntPtr data, out IntPtr counter);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhRemoveCounter(IntPtr counter);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhCloseQuery(IntPtr query);
    [DllImport("pdh.dll", ExactSpelling = true)] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhGetCounterInfoW(IntPtr counter, [MarshalAs(UnmanagedType.Bool)] bool explain, ref uint bytes, IntPtr buffer);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bytes, out uint count, IntPtr buffer);
}
