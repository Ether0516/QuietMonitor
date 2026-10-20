using QuietMonitor.Core;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
Check(CpuMath.Busy(0, 0, 0, 40, 70, 30) == 60, "CPU kernel includes idle");
Check(CpuMath.Busy(10, 20, 30, 10, 20, 30) == null, "CPU zero interval is unavailable");
Check(CpuMath.Busy(10, 20, 30, 1, 2, 3) == null, "CPU counter reset is unavailable");
Check(CpuMath.Busy(0, 0, 0, 150, 100, 0) == null, "CPU invalid idle delta rejected");
var data = GpuMath.Aggregate(new[] { new GpuLoad("a", "0:compute", 50), new GpuLoad("a", "0:compute", 40),
    new GpuLoad("a", "0:3d", 5), new GpuLoad("b", "0:compute", 80), new GpuLoad("a", "1:compute", 30) });
Check(data["a"] == 90 && data["b"] == 80, "GPU sums processes within engine, max across engines, separates devices");
Check(GpuMath.Aggregate(new[] { new GpuLoad("a", "0", double.NaN), new GpuLoad("a", "0", -5) }).Count == 0, "GPU invalid samples ignored");
Check(GpuMath.Aggregate(new[] { new GpuLoad("a", "0", 80), new GpuLoad("a", "0", 30) })["a"] == 100, "GPU saturation clamped per engine");
var parsed = GpuMath.Parse("pid_123_luid_0x00000000_0x0000ABCD_phys_0_eng_2_engtype_Compute_0", 97);
Check(parsed?.DeviceId == "0x00000000_0x0000abcd" && parsed.EngineId == "0:2" && parsed.Percent == 97, "GPU parses compute engine without 3D filtering");
Check(GpuMath.Parse("unknown-format", 100) == null, "GPU unknown instance rejected");
Check(new Reading(null, "", "").Display == "N/A" && new Reading(0, "", "").Display == "0%", "Unavailable and idle are distinct");
Console.WriteLine($"{passed} checks passed.");
