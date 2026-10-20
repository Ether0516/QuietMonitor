# Research and implementation decisions

## Evaluated projects

| Project | Findings | Decision |
| --- | --- | --- |
| [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor) | Mature Windows overlay/taskbar UI and plugins; C++; Anti-996 license | Interaction reference; no source imported |
| [PerfOverlay](https://github.com/Damianjiang/PerfOverlay) | Small MIT WPF overlay; examined GPU code filters 3D and conflates some errors with zero | Useful reference, independently implement sampling and UI |
| [Piperf](https://github.com/Pimond/Piperf) | Similar WPF overlay; README stated no explicit license | Feature reference only |
| [SystemBarIndicator](https://github.com/coldneuro/SystemBarIndicator) | MIT WinForms compact monitor with expanded charts and sensor dependencies | Layout reference only |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | MPL-2.0 hardware library; some sensors need elevation | Optional future sensors; not a current dependency |
| [Cherry Studio](https://github.com/CherryHQ/cherry-studio) / [Chatbox](https://github.com/chatboxai/chatbox) | Full AI clients | Future interaction references; no code imported |

Research checkouts and downloaded source samples were removed after documenting the findings. See THIRD_PARTY_NOTICES.md.

## GPU coverage

[Microsoft's Task Manager explanation](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/) describes API-independent GPU accounting, including CUDA, and a busiest-engine overall figure. The implementation uses public Windows PDH counters and verifies them independently; it does not claim to call Task Manager's private implementation or match every instantaneous value.

All exposed engine types are collected. NVIDIA NVML is an optional fallback, not a requirement for the Windows primary path. [NVML](https://docs.nvidia.com/deploy/nvml-api/latest/api/group__nvmlDeviceQueries.html) GPU utilization has a different activity definition and is labeled accordingly. Basic GPU activity does not prove a particular algorithm is making useful progress.

## OpenCode (future, not implemented)

The v2 CLI uses a shared background service; terminal use does not prevent API-based observation. Early local, read-only testing retrieved server/session metadata. Direct unauthenticated requests required authentication; no end-to-end task event verification was completed.

The [v2 API](https://opencode.ai/v2/docs/api) is experimental, so isolate it behind an adapter. An active-session snapshot alone does not prove success, and the event stream does not guarantee disconnected-event replay. Obtain fresh snapshots after reconnection. Do not copy v1 attach/plugin examples into a v2 integration.

## Local-first optional modules

[Obsidian Daily notes](https://help.obsidian.md/Plugins/Daily+notes) and [URI](https://help.obsidian.md/Extending+Obsidian/Obsidian+URI) offer an initial open/export route. Android home-screen widgets have separate UI/lifecycle requirements; see [Jetpack Glance](https://developer.android.com/develop/ui/compose/glance). These remain roadmap considerations, not current dependencies.

Detailed feature concepts are in ROADMAP.md; sanitized actual test results belong in VALIDATION.md, not raw machine reports.
