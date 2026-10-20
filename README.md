# Quiet Monitor · 电脑监控 MVP

Windows 半透明桌面监控，默认仅一行 CPU / GPU 使用率。无账号、无联网采样、无常驻服务或内核驱动。当前版本 0.1.0。

[源代码](https://github.com/Ether0516/QuietMonitor) · [构建状态](https://github.com/Ether0516/QuietMonitor/actions) · [反馈问题](https://github.com/Ether0516/QuietMonitor/issues)

当前为早期 MVP，完整桌面交互与更多硬件的人工验收仍在进行，详见 [验证记录](VALIDATION.md)。

## 运行

发布包解压后双击 **QuietMonitor.exe**，同目录依赖文件请一起保留。发布目标为 Windows x64，自包含版不要求另装 .NET 或 CUDA Toolkit。

- 左键拖动定位，靠近屏幕工作区边缘可吸附。
- 右键菜单打开设置、锁定穿透、恢复位置或退出；双击面板打开设置。
- 系统托盘图标提供全部恢复操作，双击图标打开设置。
- **Ctrl + Alt + M**：显示 / 隐藏。
- **Ctrl + Alt + Shift + M**：显示并解除穿透。
- 快捷键若被其他软件占用，可从托盘恢复；设置中会显示注册状态。
- 关闭设置窗口或隐藏面板不会退出；从右键/托盘菜单选择“退出”。

设置支持背景不透明度（仅背景变化）、字号、刷新间隔、置顶、锁定穿透和显卡选择。未保存的外观预览在关闭/取消设置时恢复。位置自动保存，显示布局变化后会约束到可见工作区。

默认不显示显存、温度、曲线、日历、AI 等内容。它们尚未实现，不作为本版范围。新想法记录在项目 IDEAS.md，长期规划在 ROADMAP.md。

## 指标含义

CPU 优先 Windows Processor Utility 性能计数器，失败后尝试 Processor Time / 系统时间差分；不同来源的统计口径可能有所差异。

GPU 通过 Windows GPU Engine 计数器自动采集所有引擎，包含驱动暴露的计算、3D、复制和视频等活动，不只筛选 3D。先按同一物理引擎汇总各进程，再取该显卡最忙引擎的百分比。多卡自动模式取最繁忙显卡，不加总/平均各卡百分比；可在设置中固定某张卡。

Windows GPU 统计不可用时按能力尝试 NVIDIA NVML，悬停提示与设置诊断中明确显示回退来源。NVML 与 Windows 引擎计数器是不同口径，不进行混加。数据不可用显示 N/A，初始化时短暂建立采样基线。

兼容目标为常见 Windows 10/11、正常 WDDM 2.x+ 驱动的 NVIDIA/AMD/Intel 显卡；目前实机验证范围以项目 VALIDATION.md 为准。虚拟环境、损坏计数器、非 WDDM 计算设备和部分旧驱动可能需要回退或显示 N/A。不承诺普通置顶窗口覆盖独占全屏或系统安全桌面。

## 本地数据

默认配置与轮转日志：`%LOCALAPPDATA%\QuietMonitor\`。配置文件 `settings.json`，日志 `monitor.log`。只记录采样/配置错误，无进程列表、对话正文或遥测上传。

启动参数：

```text
QuietMonitor.exe --settings
QuietMonitor.exe --data-dir "D:\QuietMonitorData"
QuietMonitor.exe --diagnose --samples 5 --output "D:\diagnostics.json"
```

诊断模式采集指定数量样本，写入 JSON 后退出，不显示悬浮窗、不更改显卡设置。报告包含设备名称、操作系统与采样来源，分享前可自行检查。

## 从源码构建

需要 .NET 10 SDK（项目提供 global.json）。执行 `./build.ps1` 编译并运行核心检查；执行 `./build.ps1 -Publish` 生成自包含发布目录和 ZIP。脚本优先使用项目 `.tools/dotnet`，没有时使用 PATH 中的 dotnet。

如果原发布目录正在运行，可使用 `./build.ps1 -Publish -PackageName QuietMonitor-preview-win-x64` 输出到另一个目录，避免文件占用。

产品源码在 `src/`，核心逻辑检查在 `tests/`。GitHub Actions 会编译、执行核心检查并生成 Windows x64 测试包；工作流成功后可从该次运行的 Artifacts 下载（需登录 GitHub）。这些测试包不等同于完成全部人工验收的正式发行版。

## 项目维护

采用 [MIT 许可证](LICENSE)，实现独立编写。运行时与第三方组件说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

- [当前实现计划](PLAN.md) / [验证记录](VALIDATION.md) / [变更记录](CHANGELOG.md)
- [想法清单](IDEAS.md) / [长期路线图](ROADMAP.md) / [开源方案调研](RESEARCH.md)
- [贡献指南](CONTRIBUTING.md)

默认界面保持 CPU / GPU 两项。后续个人功能按需启用，并在实现前确认范围。
