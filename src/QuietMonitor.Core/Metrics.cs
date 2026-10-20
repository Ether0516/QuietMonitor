using System.Globalization;
using System.Text.RegularExpressions;

namespace QuietMonitor.Core;

public sealed record Reading(double? Percent, string Source, string Detail)
{
    public string Display => Percent.HasValue
        ? Math.Clamp(Percent.Value, 0, 100).ToString("0", CultureInfo.InvariantCulture) + "%"
        : "N/A";
}

public sealed record GpuDevice(string Id, string Name);
public sealed record GpuLoad(string DeviceId, string EngineId, double Percent);
public sealed record MetricSnapshot(DateTimeOffset ObservedAt, Reading Cpu, Reading Gpu,
    IReadOnlyDictionary<string, double> GpuByDevice);

public static class CpuMath
{
    public static double? Busy(ulong idleBefore, ulong kernelBefore, ulong userBefore,
        ulong idle, ulong kernel, ulong user)
    {
        if (idle < idleBefore || kernel < kernelBefore || user < userBefore) return null;
        double total = (double)(kernel - kernelBefore) + (user - userBefore);
        double idleDelta = idle - idleBefore;
        return total <= 0 || idleDelta > total ? null : Math.Clamp(100 * (1 - idleDelta / total), 0, 100);
    }
}

public static partial class GpuMath
{
    // PID instances contribute to one physical engine. Independent engines run in parallel.
    public static Dictionary<string, double> Aggregate(IEnumerable<GpuLoad> loads) => loads
        .Where(x => double.IsFinite(x.Percent) && x.Percent >= 0)
        .GroupBy(x => (x.DeviceId, x.EngineId))
        .Select(x => new { x.Key.DeviceId, Busy = Math.Clamp(x.Sum(v => v.Percent), 0, 100) })
        .GroupBy(x => x.DeviceId)
        .ToDictionary(x => x.Key, x => x.Max(v => v.Busy), StringComparer.OrdinalIgnoreCase);

    public static GpuLoad? Parse(string instance, double value)
    {
        var match = InstanceRegex().Match(instance);
        if (!match.Success) return null;
        return new GpuLoad($"{match.Groups[1].Value}_{match.Groups[2].Value}".ToLowerInvariant(),
            $"{match.Groups[3].Value}:{match.Groups[4].Value}", value);
    }

    public static string Luid(uint low, int high) =>
        $"0x{unchecked((uint)high):x8}_0x{low:x8}";

    [GeneratedRegex(@"luid_(0x[0-9a-f]+)_(0x[0-9a-f]+)_phys_(\d+)_eng_(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex InstanceRegex();
}
