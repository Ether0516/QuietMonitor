# Project maintenance instructions

## Product scope

- Default UI shows only CPU and GPU percentages. Optional features stay disabled until explicitly enabled.
- GPU monitoring must cover every exposed engine, including compute/CUDA workloads; never filter only 3D.
- Aggregate process instances within the same physical engine, then choose the busiest engine per GPU. Do not sum independent engines or GPUs.
- Keep unavailable, warming-up and idle readings distinct. Label fallback sources and differences in meaning.
- Preserve non-activating overlay behavior and a reliable tray/shortcut escape from click-through mode.
- Record new ideas in IDEAS.md first. They do not automatically become current implementation scope. See PLAN.md and ROADMAP.md.

## Development workflow

- Inspect git status before editing. Preserve unrelated user changes.
- Use build.ps1 as the single reusable build/check/publish entry point. Avoid one-off helper scripts in the repository root.
- Run ./build.ps1 after code changes; run ./build.ps1 -Publish when changing distribution or release content.
- The console checks are real executable assertions. dotnet test does not run them: use build.ps1 or dotnet run --project tests/QuietMonitor.Checks.
- Tests should target math, aggregation, state transitions and failure boundaries; manually verify window behavior on real Windows hardware.
- Update CHANGELOG.md and VALIDATION.md with meaningful changes and actual evidence. Never describe an interrupted or unperformed check as passed.
- Use focused commits. Feature branches/PRs are preferred for substantial work after the initial import. Do not force-push, rewrite published history or merge unrelated changes.
- Maintain source/runtime separation: Windows APIs stay in the app/platform layer; platform-independent models and math stay in Core.

## Cleanup and publishing

- Keep .tools SDK/runtime caches local and ignored; they remain useful for rebuilding. Remove obsolete installers and downloaded research copies once their findings are documented.
- Never commit .local, credentials, raw diagnostic reports, local machine paths, SDKs, build output or unsigned release binaries to source history.
- Before recursive deletion/moving on Windows, resolve absolute paths and verify they are inside this workspace. Do not traverse junctions/reparse points during bulk cleanup.
- The project is independently implemented. Do not import third-party code without preserving its license and recording provenance in THIRD_PARTY_NOTICES.md.
- Keep source license separate from bundled .NET/runtime licenses. Include required notices in release packages.
- Initial open-source publication is authorized by the owner. Subsequent public commits should remain within the requested task scope; publishing a new release is an explicit task or agreed release step.
- The maintainer records ideas and performs requested maintenance during project work; no background automation is implied.
