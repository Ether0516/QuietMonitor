# Third-party notices and provenance

Quiet Monitor application source is independently implemented and licensed under MIT; see LICENSE.

## .NET / Windows Desktop runtime

Self-contained builds bundle Microsoft .NET and Windows Desktop runtime components under their own licenses. The packaging script copies the LICENSE and any THIRD-PARTY-NOTICES files supplied by the exact restored Core and Windows Desktop runtime packages into `licenses/<package>/<version>/`. Preserve that directory when redistributing. The application's MIT license does not replace their terms.

Upstream: https://github.com/dotnet/runtime and https://github.com/dotnet/windowsdesktop (WPF: https://github.com/dotnet/wpf; Windows Forms: https://github.com/dotnet/winforms).

## NVIDIA NVML

The optional fallback dynamically loads the NVIDIA NVML DLL already installed with the user's driver. No NVIDIA DLL or SDK is included in this repository or added by the packaging script. Its use remains subject to NVIDIA's applicable terms.

## Research references

TrafficMonitor, PerfOverlay, Piperf, SystemBarIndicator, LibreHardwareMonitor, Cherry Studio and Chatbox were evaluated during planning. Their source files, icons and assets were not incorporated into this application. Temporary research checkouts have been removed. RESEARCH.md retains the engineering findings and upstream links.

Any future source reuse must add the exact upstream version, files, copyright and required notices before distribution.
