# Validation status

Last updated: 2026-10-20. Scope: initial Windows x64 MVP. This document distinguishes implemented behavior from completed verification.

| Check | Evidence / status |
| --- | --- |
| Release build | Passed locally with .NET 10 SDK; zero compiler warnings/errors on the final MVP build |
| Core calculations | 10 executable checks passed: CPU idle accounting/reset, per-engine process sums, engine/device separation, compute-instance parsing, invalid samples and N/A vs zero |
| Self-contained publication | Windows x64 folder and ZIP produced; republished with source/runtime licenses in separate files. Used `-PackageName QuietMonitor-preview-win-x64` because the previous output had files in use; no separate .NET installation is required by that publish mode |
| Real Windows sampling | Windows 11 / NVIDIA GeForce RTX 3050: CPU and all-engine GPU PDH readings obtained; multiple valid samples, including 388 process-engine instances in one observation |
| Restricted environment | PDH name lookup failed in the sandbox; CPU-time and NVML fallback produced samples. Normal Windows permissions restored PDH readings |
| Settings UI | Earlier UI inspected; lock setting saved to test JSON. Contrast defect was fixed and rebuilt |
| Final full interaction pass | Incomplete: Computer Use was stopped by the user. Do not treat the final UI pass as completed |
| Mouse pass-through / recovery shortcuts | Implemented; complete cross-application input verification remains pending |
| Multi-monitor, mixed DPI, unplug/reconnect | Implemented positioning safeguards; full physical-device matrix pending |
| Sleep/resume | Implemented baseline reset; actual suspend/resume verification pending |
| Long-running CPU/memory target | Not yet measured with a controlled idle baseline or 8-hour run |
| AMD / Intel and other Windows versions | Not yet physically tested |
| Stable release readiness | Pending the applicable manual checks above |

## Reproduce

```powershell
./build.ps1
./build.ps1 -Publish
./artifacts/QuietMonitor-win-x64/QuietMonitor.exe --diagnose --samples 6 --output ./artifacts/diagnostics.json
```

Normal diagnostic mode uses the caller's current privileges; sandbox failures should not be presented as OS-wide failures. A successful diagnostic command does not itself prove that all samples are valid: inspect Completed, sample values and sources.

Keep raw reports local. Reports contain device information and can include local error details. Record sanitized conclusions here; never publish a report automatically.

## Manual checklist for the next preview

- Compare CPU and all-engine GPU trends to Task Manager during idle, CUDA compute and video/3D load, with sampling/metric differences recorded.
- Verify dragging, edge snapping, opacity and settings cancellation.
- Lock click-through over another application; recover using tray and shortcut; verify focus is preserved.
- Hide/show, relaunch the same instance, quit cleanly and verify tray/shortcut resources are released.
- Verify unplugged displays, negative monitor coordinates, mixed scaling and suspend/resume.
- Measure idle CPU and private memory with settings closed, then run a bounded soak test.
