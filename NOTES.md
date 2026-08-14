# 啥字儿（WhatFont）开发记录

Windows 字体查看小工具：读取本机字体，显示实际预览、PostScript name 与 Family name。

技术栈：.NET 10 + Avalonia 12.1.1 + SkiaSharp 3.119.4 + NativeAOT。目标平台为 Windows 10+ x64；发布版自包含，无需安装 .NET。

## 环境与约束

- 开发机使用 .NET SDK 10.0.400。
- NativeAOT 需要 VS2022 Community 的 VC++ 工具链和 MSVC linker。
- 开发机启用了 MacTypeTray，系统 UI 字体为 Noto Sans；截图中的彩色子像素边缘可能来自 MacType，不等同于位图数据损坏。
- `C:\Windows\Fonts` 中 msyh、simsun、NotoSansSC 等 CJK 字体及其注册表项均存在。
- 项目名、程序集名和 exe 文件名保留 `WhatFont`；窗口、自绘标题栏和任务栏标题显示“啥字儿”。

## 当前实现基线

- 窗口固定宽度 560px，只允许调整高度，不支持最大化。
- Win11 优先 Mica，Win10 回退 AcrylicBlur，再回退到浅色不透明背景。
- 搜索框与卡片列表左右各内缩 8px，二者边缘保持一致。
- 字体预览区域固定高 38px，使用 2x Skia 位图缩小显示。
- 滚动条有 10px 透明命中区和 5px 可见滑块；滚动时淡入，900ms 后淡出。
- Family 与 PostScript name 各有独立的整行复制按钮；Family 优先展示，复制图标统一右对齐，成功或失败均显示 toast。
- 应用图标为透明背景上的纯黑 `F`，包含常用 ICO 尺寸。

## 字体与 OpenType

### 1. Avalonia 无法可靠地从 file URI 加载磁盘字体

**现象**：使用 `FontFamily(file://...)` 后，所有卡片看起来都像默认字体。

**根因**：Avalonia 12 的文本控件没有按预期加载枚举得到的任意磁盘字体文件，渲染时发生字体回退。

**处理**：不再让 Avalonia 文本排版器负责预览。使用 `SKTypeface.FromFile` 直接打开字体，再用 SkiaSharp 渲染透明位图并写入 `WriteableBitmap`。

### 2. OpenType name 表必须使用 stringOffset

**现象**：Family/PostScript 行显示方块、乱码或无意义字符。

**根因**：name record 中的 `offset` 不是相对于 name 表起点，而是相对于 name 表头给出的 `stringOffset`。

**处理**：字符串真实位置按 `stringOffset + offset` 计算，并在读取前检查 `offset + length` 是否越过 name 表边界。

### 3. TTC 的 face offset 和 table offset 都从文件起点计算

**现象**：TTF/OTF 正常，但 `msyh.ttc` 等 TTC 字体显示乱码；错误名称中的控制字符还会把一张卡片撑得很高。

**根因**：代码把 TTC face offset 加上了 offset 数组自身的位置，又把 table offset 加到了 face offset 上。OpenType TTC 规范中，这两类偏移都相对于整个文件起点。

**处理**：face offset 直接使用 TTC header 中的值；定位 name table 时直接使用 table directory 的 offset。用 `msyh.ttc` 验证结果为 `MicrosoftYaHei / Microsoft YaHei`。

### 4. 不要把所有旧平台编码都当 UTF-16BE

**现象**：少数字体仍可能产生乱码或控制字符。

**根因**：Unicode/Windows name record 通常使用 UTF-16BE，但 Macintosh 平台只有 encoding 0 可按 MacRoman 解码；其他旧 Mac 编码不能直接当 UTF-16BE。

**处理**：仅解码支持的平台/编码，过滤控制字符；名称优先级保持 Windows > Unicode > Macintosh。无法安全解码的记录直接跳过。

### 5. 字符缺失方框不一定是渲染错误

**现象**：某些西文字体的 `汉字` 部分显示方框。

**原因**：预览使用字体文件本身，不做跨字体 fallback。字体不包含 CJK glyph 时，方框真实反映其字符覆盖范围。

**结论**：这是预览功能的一部分，不应为了消除方框而引入系统字体回退。

## 预览位图

### 6. MeasureText 紧凑边界不能代替完整行高

**现象**：预览文字的上沿、下沿或带重音字符被裁切。

**根因**：`MeasureText` 的 bounds 只覆盖当前字符串的紧凑外框，不能保证容纳字体完整 ascent/descent。

**处理**：高度使用 `SKFont.Metrics.Ascent/Descent`，宽度继续使用测量 bounds，并在四周加入 6px 逻辑内边距。

### 7. 2x WriteableBitmap 的 DPI 元数据会造成二次尺寸解释

**现象**：位图像素内容完整，但 Avalonia `Image` 只显示左上半幅；改为 `Stretch="Uniform"` 后又可能把预览放大一倍。

**根因**：位图按 2x 像素渲染，同时写入 192 DPI 后，Avalonia 按一半尺寸布局，却仍可能按原始像素绘制。

**处理**：保持 2x 像素用于超采样，但 `WriteableBitmap` 元数据设为 96 DPI；`Image` 固定高 38px，并使用 `Stretch="Uniform"` 缩小整张位图。

## 窗口与视觉

### 8. 控件主题与 Mica 明暗色可能不一致

**现象**：深色主题产生白字，但 Windows 返回浅色 Mica，最终形成白字、白卡片和浅背景混在一起的“全白”界面。

**根因**：Avalonia 控件的 `RequestedThemeVariant` 与 Windows backdrop 的实际明暗并不保证自动一致；窗口背景又是透明的。

**处理**：产品统一为 Light 主题；加入浅色半透明内容底层、接近不透明的白色卡片、深色文字、边框和轻投影，使 Mica/Acrylic/fallback 三种情况下都可读。

### 9. 无边框窗口需要同时声明拖动区与按钮区

**现象**：自绘标题栏可能无法拖动，或最小化/关闭按钮点击被系统拖动区域吞掉。

**处理**：使用 `WindowDecorations="BorderOnly"` 和 `ExtendClientAreaToDecorationsHint`；标题栏容器标记为 `ElementRole="TitleBar"`，按钮标记为 `DecorationsElement`。

**附带坑**：关闭按钮内部实际使用的是 `Path`，悬停样式若写成 `PathIcon` 不会命中，并且给 `PathIcon` 设置 `Stroke` 会导致 XAML 编译错误。

### 10. TextBox 固定高度不代表文字会自动垂直居中

**现象**：搜索框高度为 40px，但输入文字和 placeholder 靠上。

**根因**：`Height` 与 `Padding` 只控制外框和内边距，模板的内容对齐仍使用默认值。

**处理**：显式设置 `VerticalContentAlignment="Center"`。搜索框和 `ItemsControl` 同时使用 `Margin="8,0"`，避免输入框比卡片宽一圈。

### 11. AllowAutoHide 解决 overlay，但不会在内容滚动后自动淡出

**现象**：默认滚动条占用独立布局列，卡片宽度被挤压；仅设置 `AllowAutoHide=True` 后虽然成为 overlay，滑块仍长期可见。

**根因**：Avalonia Fluent 的 `AllowAutoHide` 会让 `ScrollContentPresenter` 跨过滚动条列，但 `ScrollBar.IsExpanded` 主要由指针进入/离开滚动条控制，不会因为内容区滚轮事件自动执行 macOS 式显隐。

**处理**：

- ScrollViewer 启用 `AllowAutoHide`，卡片不再为滚动条预留宽度。
- 隐藏轨道、上下箭头和背景，保留 10px 透明命中区及 5px 圆角 Thumb。
- `ScrollChanged` 时给 ScrollViewer 添加 `scrolling` class，900ms 后移除；Thumb 用 180ms opacity transition 淡入淡出。
- 卡片列表左右内缩 8px，让可见滑块完整落在右侧边距中，不覆盖卡片边框。

### 12. 窗口标题、自绘标题和任务栏文字是两套显示

**现象**：只改自绘标题栏 `TextBlock` 后，任务栏仍显示旧名称。

**根因**：任务栏缩略图和窗口列表读取 `Window.Title`，自绘标题则只是普通 TextBlock。

**处理**：两处同时设置为“啥字儿”。项目目录、程序集与 exe 仍使用 `WhatFont`，避免无必要的构建路径变更。

### 13. 任务栏图标需要同时绑定窗口与 exe

**现象**：仓库里已有 ICO，但任务栏仍没有图标；或资源管理器有图标，运行窗口仍为空白。

**根因**：`AvaloniaResource Include="Assets\**"` 只会嵌入资源，不会自动把它设置为 Windows 应用图标。

**处理**：

- `ApplicationIcon` 设置 exe 资源图标。
- `Window.Icon` 设置运行窗口图标，覆盖任务栏和 Alt+Tab 场景。
- 自绘标题栏复用同一 ICO。
- ICO 必须包含常用尺寸；当前图标由 `tools/generate_icon.py` 生成，为透明背景上的纯黑 `F`。

## 交互

### 14. PointerPressed 不适合表示“完成一次点击”

**现象**：在列表中按下鼠标准备拖动/滚动时，也可能触发复制；剪贴板写入未等待完成就显示成功 toast。

**根因**：`PointerPressed` 在按下阶段立即触发；`Clipboard.SetTextAsync` 若不 await，失败会变成未观察任务，提示也可能是假成功。

**处理**：名称操作使用独立 `Button.Click`，不再把复制绑定在卡片按下事件上；await 剪贴板写入后再显示“已复制”，异常时显示“复制失败”。Family/PS 按钮、剪贴板内容与 toast 均已通过 UI 自动化验证。

### 15. 重复 toast/滚动事件要正确取消并释放 CTS

**现象**：快速连续点击或持续滚动会产生多个延迟任务，旧任务可能隐藏新提示，长期运行还会积累 `CancellationTokenSource`。

**处理**：新事件先取消旧 CTS；finally 中仅由当前 CTS 清理可见状态/class，并在最后 Dispose。使用 `ReferenceEquals` 防止旧任务覆盖新任务状态。

## Windows 与 NativeAOT

### 16. Windows-only 项目应使用 Windows TFM

**现象**：注册表 API 产生 CA1416 平台兼容警告。

**根因**：项目原先使用通用 `net10.0`，分析器不知道调用只发生在 Windows。

**处理**：TFM 改为 `net10.0-windows`，与产品的 Windows 10+ 约束一致；普通构建达到 0 警告、0 错误。

### 17. Avalonia + Skia NativeAOT 不是单 exe 发布

**现象**：`PublishAot=true`、`PublishSingleFile=true` 和 `IncludeNativeLibrariesForSelfExtract=true` 仍输出多个 DLL。

**根因**：NativeAOT 只原生编译托管应用；Avalonia/Skia 的原生库不会被 .NET single-file bundler 合并到 AOT exe。

**结论**：发布目录必须保留 4 个文件：

- `WhatFont.exe`，约 20.5 MiB
- `av_libglesv2.dll`
- `libHarfBuzzSharp.dll`
- `libSkiaSharp.dll`

合计约 38.5 MiB。它们是自包含部署，不依赖已安装的 .NET，但不能只复制 exe。

### 18. StripSymbols 在 Windows NativeAOT 下不足以去掉 PDB

**现象**：设置 `StripSymbols=true` 后，发布目录仍有应用和第三方 PDB；普通 `AfterTargets="Publish"` 删除还可能早于 NativeAOT 的 `_CopyAotSymbols`。

**根因**：Windows NativeAOT 有独立的 native symbol 生成/复制阶段；目标内部对 `DebugType` 使用小写 `none` 判断，并可能把 obj 中的旧 PDB 再复制到 publish。

**处理**：Release 配置同时设置：

- `DebugType=none`（小写）
- `DebugSymbols=false`
- `CopyOutputSymbolsToPublishDirectory=false`
- Publish 后删除残留的 `$(PublishDir)**\*.pdb`

最终发布目录已验证为 4 个文件、0 个 PDB。

### 19. 不要并发运行两个 NativeAOT publish

**现象**：`ilc` 报错无法访问 `obj\...\native\WhatFont.sourcelink`，提示文件被另一进程占用。

**根因**：一次短超时返回后，原生编译子进程仍可能继续运行；立刻启动第二次 publish 会让两个 `ilc` 写同一中间目录。

**处理**：确认旧的 dotnet/ilc/link 进程退出后再重试；同一 RuntimeIdentifier 和 Configuration 只运行一个 publish。

### 20. 高 DPI 下的窗口截图会被调用方 DPI 虚拟化

**现象**：`PrintWindow` 截图只有窗口左侧约三分之二，右侧被裁掉；应用本身实际显示正常。

**根因**：PowerShell 进程不是 per-monitor DPI aware，`GetWindowRect` 返回虚拟尺寸，而 `PrintWindow` 写入物理像素。

**处理**：截图线程先调用 `SetThreadDpiAwarenessContext(PER_MONITOR_AWARE_V2)`，再读取窗口尺寸并分配 bitmap。在 150% DPI 下完整截图约为 862x1032。

### 21. 便携 ZIP 必须在 NativeAOT 最终复制和清理之后生成

**风险**：如果直接挂在普通 `Publish` 后打包，ZIP 可能包含托管占位 exe、PDB 或上一次生成的 ZIP；如果 ZIP 放进 `publish` 目录，下次还可能把旧 ZIP 嵌套进去。

**处理**：`CreatePortableArchive` 挂在 NativeAOT 的 `CopyNativeBinary` 之后，并依赖 `RemovePublishSymbols`。归档固定输出到 `$(MSBuildProjectDirectory)\Publish`，不再跟随深层 `PublishDir`；目标先用 `MakeDir` 创建目录，再生成 `WhatFont-$(Version)-$(RuntimeIdentifier)-portable.zip`。ZIP 根目录只包含 publish 中的 4 个运行文件。

项目默认 `Version=1.0.0`，可在发布时用 `-p:Version=x.y.z` 覆盖；平台名直接使用 `RuntimeIdentifier`，当前为 `win-x64`。最终分发包路径为 `WhatFont\Publish\WhatFont-<版本>-<平台>-portable.zip`，该目录由仓库根目录的 `.gitignore` 排除。

### 22. Family name 与 PostScript name 不能共用一个含糊的复制入口

**现象**：整张卡片点击时只复制 PostScript name，但 VS Code 等编辑器的字体配置通常需要 Family name，用户还无法直接复制它。

**处理**：取消整卡复制，在卡片中提供独立的 Family 和 PS 行；Family 放在上方并使用更强的文字层级。两行各自拥有复制图标、tooltip 和明确的 toast，避免复制结果与用户预期不一致。

VS Code 的 `editor.fontFamily`、`terminal.integrated.fontFamily` 等设置通常使用 Family name；PostScript name 只用于明确要求该名称类型的软件或工作流。

### 23. `StackPanel` 中的按钮不一定会形成稳定的整行点击区

**现象**：Family/PS 按钮虽然设置了 `HorizontalContentAlignment="Stretch"`，初版复制图标仍紧跟在名称后面，不同字体的图标横向位置不一致，按钮自动化边界也只覆盖内容所需宽度。

**根因**：内容对齐只控制 Button 内部，不保证父布局按卡片可用宽度安排 Button；纵向 `StackPanel` 的测量结果可能保留子元素的期望宽度。

**处理**：卡片内容改用带三行定义的 `Grid`，按钮同时设置 `HorizontalAlignment="Stretch"` 和 `HorizontalContentAlignment="Stretch"`。名称放在星号列，复制图标放在固定宽度的末列，因此两行点击区等宽且图标统一贴齐右侧。

### 24. Avalonia 的 Path 属性和剪贴板扩展命名空间容易与其他 UI 框架混淆

**现象**：复制图标使用 `StrokeLineJoin` 时 XAML 编译报 `AVLN2000`；移除看似未使用的 `Avalonia.Input.Platform` 后，`IClipboard.SetTextAsync` 又报 `CS1061`。

**根因**：Avalonia `Path` 的连接样式属性名是 `StrokeJoin`；`SetTextAsync` 是 `Avalonia.Input.Platform` 命名空间提供的扩展方法，并非 `IClipboard` 自身的实例方法。

**处理**：图标路径使用 `StrokeJoin="Round"`，并保留 `using Avalonia.Input.Platform;`。复制按钮另外设置 `AutomationProperties.Name`，确保 UI 自动化和辅助工具能区分 Family 与 PostScript 操作。

### 25. tag 自动发布必须让工作流存在于 tag 指向的提交中

**风险**：先给旧提交打 tag、再补 GitHub Actions 文件时，tag push 不会运行新工作流；Linux runner 也无法完成 Windows NativeAOT 链接。工作流若没有 `contents: write` 权限，则能构建但无法创建 Release。

**处理**：先提交并推送 `.github/workflows/release.yml`，再给该 HEAD 创建 `v<版本>` tag。工作流使用 `windows-latest`、.NET 10 和仓库内置 `GITHUB_TOKEN`，从 tag 提取 `Version`，验证 ZIP 只包含 4 个运行文件后执行 `gh release create`。

tag 采用 `v1.2.3` 形式；生成的 Release 标题为 `WhatFont v1.2.3`，附件名为 `WhatFont-1.2.3-win-x64-portable.zip`。

## 已完成验收

- [x] `dotnet build`：0 warning、0 error
- [x] 搜索 Family/PostScript/路径
- [x] TTF、OTF、TTC 名称解析；`msyh.ttc` 名称正确
- [x] 预览字形完整，无 DPI 裁切
- [x] Family/PS 独立复制、剪贴板内容、成功/失败 toast
- [x] Family/PS 整行点击区等宽，复制图标右对齐，自动化名称可区分
- [x] 浮层滚动条静止隐藏、滚动显示、延迟淡出，卡片宽度不变化
- [x] 搜索框垂直居中并与卡片等宽
- [x] 自绘标题、Window.Title、任务栏文字均为“啥字儿”
- [x] Debug 和 NativeAOT exe 均包含透明背景纯黑 `F` 图标；运行窗口大小图标句柄有效
- [x] NativeAOT 实际启动并响应，发布目录 4 个文件、0 个 PDB
- [x] 发布后在 `WhatFont\Publish\` 自动生成 `WhatFont-<版本>-<平台>-portable.zip`
- [x] 推送 `v<版本>` tag 后由 GitHub Actions 自动创建 Release 并上传 ZIP
- [x] `dumpbin /dependents` 检查：静态依赖均为 Windows 系统 DLL；随包的 3 个原生 DLL 已保留

## 待办与剩余风险

- [ ] 在真实 Windows 10 环境实测 AcrylicBlur；当前只在 Windows 11 验证 Mica 和 fallback。
- [ ] 如果未来升级 Avalonia，重新核对 Fluent `ScrollViewer`/`ScrollBar` 模板中的 PART 名称；当前样式依赖 `PART_VerticalScrollBar`、`Root`、`VerticalRoot` 和 `TrackRect`。
- [ ] 如果未来更换预览字符串，继续保留无 glyph fallback 的行为，并重新检查极端字体 metrics。
