# Current implementation plan

Quiet Monitor is a Windows desktop widget project. The current MVP defaults to exactly two values: CPU and GPU utilization.

## Implemented scope

- C# / .NET 10 / WPF, ordinary user privileges.
- Background opacity, topmost non-activating overlay, dragging, edge snapping and saved position.
- Click-through lock with tray and keyboard recovery paths.
- CPU PDH sampling and bounded fallback; all exposed GPU engines with per-engine aggregation and busiest-device auto selection.
- Optional NVIDIA NVML fallback with explicit source labels, no redistributed vendor DLLs.
- Single-instance settings activation, local configuration/logs and diagnostic mode.
- A single build.ps1 entry point and executable Core checks.

No temperature, memory, graph, calendar, task list or AI content is added to the default view. New ideas enter IDEAS.md before an implementation decision.

## Immediate work

1. Finish the actual manual checks listed in VALIDATION.md, including click-through recovery and focus behavior.
2. Measure idle CPU/private memory and validate suspend/resume and mixed-DPI positioning.
3. Gather AMD/Intel/multi-GPU compatibility reports; fix data-quality and device-selection issues before adding widgets.
4. Publish a preview package with accurate release notes after applicable checks; do not mark unverified behavior as passed.

## Architecture

- QuietMonitor.Core: platform-independent snapshots, CPU math and GPU aggregation/parsing.
- QuietMonitor.App/Platform: Windows PDH, DXGI, NVML and sampling lifecycle.
- QuietMonitor.App: WPF overlay/settings, tray, shortcuts and local configuration.
- tests/QuietMonitor.Checks: executable regression checks without a third-party test framework.

The GPU primary path reads all available engines, never only 3D. Sum process instances within an adapter/physical engine, then take the busiest engine. Multiple GPUs are not added or averaged. Missing data is N/A, never a fabricated zero. Actual driver/device support and fallback meanings are documented.

## Later iterations

Optional agent status/chat, Pomodoro, tasks, daily records, Obsidian integration and Android widgets remain roadmap proposals. See ROADMAP.md. They are separately enabled modules and do not block the basic monitor.

## Maintenance

See AGENTS.md and CONTRIBUTING.md. Use CHANGELOG.md for delivered changes, VALIDATION.md for evidence and IDEAS.md for the backlog. Keep SDKs, test settings, diagnostics and build outputs outside Git history.
