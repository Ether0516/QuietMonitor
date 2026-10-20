# Contributing

Quiet Monitor starts with a compact Windows CPU/GPU overlay. New capabilities are optional; keep the default display minimal.

## Build and check

Install .NET 10 SDK and PowerShell, clone the repository, then run:

```powershell
./build.ps1
./build.ps1 -Publish
```

The first command builds the application and executes the Core check program. The second also creates a self-contained Windows x64 folder and ZIP under artifacts/. No vendor GPU SDK or CUDA Toolkit is required to build.

## Report a problem

Use the bug report template. Include Windows version, GPU/driver, displayed sampling source, reproduction steps and expected/actual result. Diagnostics are optional: inspect them for device IDs, local paths and other personal data before posting. Do not upload tokens, raw logs from unrelated apps or personal notes.

## Propose a feature

Start with the feature template and explain the concrete use case, a minimal UI proposal and how it remains optional. Ideas enter IDEAS.md; only agreed work enters the current plan. A proposal does not need to include implementation.

## Submit changes

Use a focused branch and PR. Describe behavior, relevant validation and remaining limitations. Run build.ps1 for code changes and test real Windows interaction where relevant. Keep platform calls out of Core. Prefer a regression check for meaningful calculation/failure bugs; avoid tests that merely duplicate UI wiring.

## Release process

Update version, CHANGELOG.md and VALIDATION.md; build with -Publish; inspect package contents and licenses. Complete relevant manual checks before tagging a stable release. Source CI does not validate real GPU hardware, click-through, DPI or sleep recovery. Preview releases must state remaining validation gaps.
