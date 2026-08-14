# 啥字儿（WhatFont）

一个 Windows 字体查看小工具：枚举本机安装的全部字体，逐张卡片展示字体实际渲染效果，并显示每款字体的 **PostScript 名称** 与 **Family 名称**。项目名和 exe 文件名保留 `WhatFont`，窗口及任务栏标题显示“啥字儿”。

## 特性

- 无边框窗口 + 自绘标题栏，浅色毛玻璃背景（Win11 Mica / Win10 亚克力自动降级）
- 每款字体一张卡片：顶部为实际渲染预览，下方依次显示 Family name 与 PostScript name
- 搜索过滤（Family / PostScript / 文件路径）
- Family 与 PostScript 名称各有独立的整行复制入口（Family 优先展示，带 toast 提示）
- 浮层式细滚动条：不占用卡片宽度，滚动时显示并自动淡出
- 固定宽度 560px，仅支持调整高度，无最大化
- NativeAOT 自包含发布，无需安装 .NET 运行时，Windows 10+ 64 位

## 技术栈

| 层 | 选型 |
|---|---|
| 运行时 | .NET 10（NativeAOT，`PublishAot=true`，无需安装 .NET） |
| UI 框架 | Avalonia 12.1.1（Fluent 主题，编译绑定） |
| 预览渲染 | SkiaSharp 3.119.4（`SKTypeface.FromFile` 直读字体文件渲染位图） |
| 字体枚举 | 注册表 `HKLM/HKCU\...\CurrentVersion\Fonts` + 字体目录扫描 |
| 名称解析 | 自研 OpenType `name` 表解析器（零第三方依赖，支持 TTF/OTF/TTC） |
| MVVM | CommunityToolkit.Mvvm 8.4.2 |

## 使用

- VS Code 的 `editor.fontFamily`、`terminal.integrated.fontFamily` 等常用字体设置通常需要 **Family name**，点击卡片上方的 `Family` 行复制。
- 仅在目标软件明确要求 PostScript name 时，点击下方的 `PS` 行复制。
- 每次复制成功后，底部 toast 会同时显示名称类型和复制内容，便于确认没有选错。

## 构建与发布

```bash
# 开发调试（需 .NET 10 SDK）
dotnet run --project WhatFont/WhatFont.csproj

# 发布 NativeAOT 版本（需 VS2022 C++ 工具链）
dotnet publish WhatFont/WhatFont.csproj -c Release -r win-x64 -p:PublishAot=true

# 指定发布版本
dotnet publish WhatFont/WhatFont.csproj -c Release -r win-x64 -p:PublishAot=true -p:Version=1.2.3
```

NativeAOT 原始发布文件位于：`WhatFont\bin\Release\net10.0-windows\win-x64\publish\`。

分发时需保留以下 4 个文件（合计约 38.5 MiB）：

- `WhatFont.exe`
- `av_libglesv2.dll`
- `libHarfBuzzSharp.dll`
- `libSkiaSharp.dll`

NativeAOT 不会把 Avalonia/Skia 的原生库合并进 exe；项目会在发布结束后自动移除 PDB 调试符号。

发布成功后还会在 `WhatFont.csproj` 旁边的 `Publish` 目录自动生成最终分发 ZIP：

`WhatFont\Publish\WhatFont-<版本>-win-x64-portable.zip`

默认版本为 `1.0.0`。`WhatFont\Publish\` 已加入 `.gitignore`；ZIP 根目录直接包含上述 4 个运行文件，用户解压后运行 `WhatFont.exe`。

## 项目结构

```
WhatFont/
├── Assets/
│   └── whatfont.ico          # 透明背景、纯黑 F 的多尺寸应用图标
├── Fonts/
│   ├── FontEnumerator.cs      # 注册表 + 目录字体枚举
│   ├── NameTableParser.cs     # OpenType name 表解析（nameID 6/16/1）
│   └── FontPreviewRenderer.cs # SkiaSharp 预览位图渲染
├── ViewModels/
│   ├── MainViewModel.cs       # 加载、搜索过滤、状态
│   └── FontItem.cs            # 卡片数据模型
└── Views/
    └── MainWindow.axaml(.cs)  # 无边框窗口、自绘标题栏、卡片列表
```

图标源由 `tools/generate_icon.py` 确定性生成，修改图标后需重新运行该脚本并重新发布。

## 更多开发记录

见 [NOTES.md](NOTES.md)。
