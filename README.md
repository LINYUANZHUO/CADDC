# 一只猫dc

面向 AutoCAD 2017–2027 x64 的 CAD 工具箱插件。项目按 AutoCAD 运行时分别生成 .NET Framework 4.6.2、4.7、4.8、.NET 8 和 .NET 10 插件程序集。

## 当前状态

- 已建立可运行的 AutoCAD 插件骨架，并由用户在 AutoCAD 2026 内验证加载成功。
- 已建立 AutoCAD 2017–2027 十一条精确版本分流的 Autodesk ApplicationPlugins `.bundle` 打包结构，避免用高版本托管程序集冒充低版本兼容。
- 当前已用对应托管程序集完成 AutoCAD 2017–2027 十一个版本的零警告编译。
- 已提供最小加载检查命令 `DC_CMD_STATUS`。
- 已建立 AutoCAD 2026 Core Console 主机冒烟门禁：自动加载插件，在固定 DWG 中执行 `DC_CMD_STATUS` 与 `DC_CMD_BOARD_DRAW`，并断言板件为闭合四顶点多段线。
- 已实现第一版可停靠工具箱：左侧一级导航显示已实现的“快捷键”“插入图片”“批量打印”“门板抽面”和“五金件”，可拖动分隔条调整宽度，较长功能名自动换行。
- “快捷键”模块提供“重命名快捷键”和“CAD命令大全”两个标签页。
- 重命名页支持搜索，并可编辑功能说明、默认快捷键、自定义快捷键和禁用默认状态；只有底层命令保持只读。
- 已实现“添加映射”“删除选中”“导出 CSV”：新增行默认使用“新功能 / NEWCMD / NEWCMD / NEWCMD”；系统映射受保护，仅用户新增映射可以确认后删除；导出结果为 UTF-8 `.csv` 文件。
- CAD 命令大全内置 7 类 98 条常用命令，支持按快捷键、完整命令、功能说明和分类搜索。
- “插入图片”模块支持一次选择多张 JPG/JPEG/PNG/BMP/GIF 图片、指定图中起点并向右依次嵌入 OLE；可设置图片间距，并可按像素高度等比统一尺寸。
- “批量打印”模块提供 DCD1-DCD4 选框/布局出纸与 PDF 工作流、图框绘制、图框识别设置、任务清单、批量份数/纸张、预览和逐项删除。
- 独立 PDF 工具支持扫描文件夹、显示页数、全选/反选/清空，并按列表顺序合并所选 PDF。
- “门板抽面 → 立面门板”支持按柜体内空左下角、右上角动态预览并生成门板；自动铰链按柜内高度确定数量和基础位置，再把上、下盖板延伸值分别补入距顶、距底偏移，并按柜内水平层板避让。铰链参数、单独插入和类型标记入口已集中到同一页面；类型文字默认高度和距铰链圆间距均为 `35`，文字向对应门板内部展开。
- “门板抽面 → 俯视门板”使用独立参数：门板厚度默认 `18`、离柜体前沿默认 `2`、门缝默认 `3`。默认“门缝固定、门板均分”，常用门缝提供 `0.5–5` 的半毫米档位，同时允许自由输入并严格保留输入值；可设置起点/终点盖量，并只读显示最近一次实际门缝。依次指定柜体前沿起点和终点后，命令自动约束为水平或垂直方向；鼠标所在侧决定门板生成侧，`R` 翻转，`W/S` 或 `+/-` 调整门数，左键确认后仅生成关闭状态的门板矩形。
- “墙板”模块已提供格栅板、洞洞板和背景墙。背景墙支持 1–5 排从下往上配置、等分或稳定随机宽度、可选俯视/侧视图；上下排默认无缝衔接，按需可使用“缝隙”值保留垂直排缝；俯视图默认只生成最底排，也可切换为全部排。启用卡槽后，俯视大板保持与立面板相同的宽度和板缝，卡槽以每条板缝中心为中心覆盖该缝（`25×5`），每排最后一块板不生成悬空卡槽。
- 重命名表已移除“分类”列；两张表的列宽都可拖动调整，但不能小于当前表头和内容所需宽度，面板变窄时改用横向滚动显示完整内容。
- 当前仍是早期可用版本，不是功能完善的完整工具箱。

## 已知限制

- AutoCAD 2026 已完成真实 Core Console 主机冒烟验证；AutoCAD 2017–2025、2027 当前只完成对应 SDK 的编译验证，尚未在这些版本的完整 AutoCAD 主机中手工验收。
- AutoCAD 2017–2023 的 R21.0–R24.2 引用程序集来自 Autodesk 官方 NuGet 的精确版本包；获取脚本同时校验固定包 SHA-256、NuGet 仓库签名和固定 DLL SHA-256，并对具备 Authenticode 的文件验证 Autodesk 签名。AutoCAD 2017 官方包内有四个 DLL 本身未签名，因此由已验签的官方 NuGet 包与固定 DLL 哈希共同约束。这些专有文件只保存在本地 `lib\AutoCAD`，不会提交到仓库或打入插件包。
- AutoCAD 2017/2018 产物目标为 .NET Framework 4.6.2，安装环境必须具备 4.6.2 或更高的 .NET Framework；2019/2020 产物目标为 4.7。插件支持范围不改变各 AutoCAD 版本自身的 Windows 支持边界。
- Release 构建已统一各运行时所需的 Windows Desktop 程序集引用；已验证的 AutoCAD 2017–2027 构建均为 0 警告、0 错误。
- 核心逻辑、静态布局契约、插件加载和基础板件命令已有自动化验证；WPF 面板、鼠标交互、打印机与 PDF 工作流仍需在完整 AutoCAD 中验收。

## 项目约定

- 产品显示名称：`一只猫dc`
- 内部程序集前缀：`YiZhiMao.Dc`
- 底层 AutoCAD 命令前缀：`DC_CMD_`
- 目标支持范围：纯 AutoCAD 2017–2027，Windows x64；2017–2018 使用 .NET Framework 4.6.2，2019–2020 使用 4.7，2021–2024 使用 4.8，2025–2026 使用 .NET 8，2027 使用 .NET 10
- 原雷神工具箱仅作为功能和兼容性研究样本，不复制其账号授权、品牌或受保护实现。

## 构建

```powershell
dotnet build .\CADDC.sln -c Debug
```

默认构建使用本机 AutoCAD 2026，仅生成开发程序集，不会覆盖正式多版本 bundle。需要单独生成仅供 2026 开发验证的 R25.1 bundle 时显式执行：

```powershell
dotnet build .\CADDC.sln -c Debug /p:BuildBundle=true
```

开发 bundle 输出到：

```text
artifacts\YiZhiMaoDc.bundle
```

如果 AutoCAD 安装目录不同：

```powershell
dotnet build .\CADDC.sln -c Debug /p:AutoCADDirectory="D:\Autodesk\AutoCAD 2026"
```

首次准备 AutoCAD 2017–2023 引用程序集前，请先阅读 Autodesk ObjectARX 许可，然后执行：

```powershell
.\packaging\fetch-autocad-reference-assemblies.ps1 -AcceptAutodeskLicense
```

下载脚本只访问 NuGet 官方 API，精确锁定 Autodesk 发布的 `AutoCAD.NET`、`AutoCAD.NET.Core`、`AutoCAD.NET.Model`，并校验包与 DLL。GitHub 上的 Autodesk ADN 向导只用于参考工程结构，不作为 DLL 下载源。引用 DLL 只用于编译，受 Autodesk ObjectARX 许可约束，不会进入最终 bundle。完整来源与许可边界见 [`docs/autocad-2017-2020-sdk-sources.md`](docs/autocad-2017-2020-sdk-sources.md)和 [`docs/autocad-sdk-sources.md`](docs/autocad-sdk-sources.md)。

验证 AutoCAD 2017–2027 完整编译矩阵：

```powershell
.\tests\autocad-version-matrix.ps1 -Configuration Release
```

正式生成 AutoCAD 2017–2027 完整 bundle：

```powershell
.\packaging\build-multiversion-bundle.ps1 -Configuration Release
```

默认使用本地 `lib\AutoCAD\R21.0` 至 `R24.2`，以及相邻 `DCPlugin\lib` 中的 R24.3、R25.0、R25.1、R26.0。也可以显式覆盖每个版本的引用目录：

```powershell
.\packaging\build-multiversion-bundle.ps1 -Configuration Release `
  -AutoCAD2017Directory "路径\R21.0" `
  -AutoCAD2018Directory "路径\R22.0" `
  -AutoCAD2019Directory "路径\R23.0" `
  -AutoCAD2020Directory "路径\R23.1" `
  -AutoCAD2021Directory "路径\R24.0" `
  -AutoCAD2022Directory "路径\R24.1" `
  -AutoCAD2023Directory "路径\R24.2" `
  -AutoCAD2024Directory "路径\R24.3" `
  -AutoCAD2025Directory "路径\R25.0" `
  -AutoCAD2026Directory "路径\R25.1" `
  -AutoCAD2027Directory "路径\R26.0"
```

脚本在缺少任一版本 SDK 时会拒绝生成正式 bundle，并输出 SHA-256 文件清单。

## 安装、升级与卸载

- 安装程序默认安装到当前用户的 `%APPDATA%\Autodesk\ApplicationPlugins`；也可选择所有用户的 `%PROGRAMDATA%\Autodesk\ApplicationPlugins`，仅此模式申请管理员权限。为保证 AutoCAD 自动加载，不支持任意安装目录。
- 安装前如检测到 AutoCAD 正在运行，可重新检测、确认已保存图纸后强制关闭，或取消安装。
- 升级会先完整备份旧 bundle，再启用新版本。`Modules` 内用户新增或修改的模板、LISP 和五金资源会自动恢复；与新版同名的内置文件以新版为准，旧文件仍保留在完整备份中。
- 快捷键、打印和工具箱配置位于 `%APPDATA%\YiZhiMaoDc`，升级不会覆盖。
- 安装或卸载中途失败会自动恢复到操作前状态，并在 `%LOCALAPPDATA%\YiZhiMaoDc\Installer\Logs` 写入日志。
- 卸载默认只删除程序文件并保留配置与自定义资源；只有明确勾选二次确认项才删除当前 Windows 用户的数据。

## 验证

```powershell
dotnet run --project .\tests\YiZhiMao.Dc.Core.Tests\YiZhiMao.Dc.Core.Tests.csproj -c Release --no-restore
.\tests\no-autocad-reference-conflicts.ps1
.\tests\autocad-version-matrix.ps1
.\tests\autocad-core-smoke.ps1
```

主机冒烟脚本默认先构建 Release，再调用 `D:\Autodesk\AutoCAD 2026\accoreconsole.exe`。它只读打开 `Modules\DimTemplate\DimTemplate.dwg`，退出时放弃全部修改，日志保存到 `artifacts\host-smoke`。门禁会验证插件状态、板件闭合几何，以及固定图框样本的散线识别、重复消除、嵌套排除和阅读顺序；已完成构建时可追加 `-SkipBuild`。

## 当前命令

| 命令 | 用途 |
| --- | --- |
| `DCS` | 打开“一只猫dc”可停靠面板的默认快捷键。 |
| `DC_CMD_PANEL` | 打开面板的底层命令。 |
| `DC_CMD_STATUS` | 输出插件名称和版本，用于验证程序集已经被 AutoCAD 正确加载。 |
| `DCOLE` | 批量插入图片的默认快捷键。 |
| `DC_CMD_IMAGE_BATCH` | 批量插入图片的底层命令。 |
| `DCDY` / `DC_CMD_BATCH_PRINT` | 打开批量打印模块。 |
| `DCD1` / `DC_CMD_PRINT_PAPER` | 识别选中的图框并打开纸张打印中心。 |
| `DCD2` / `DC_CMD_PRINT_PDF` | 识别选中的图框并打开 PDF 输出中心。 |
| `DCD3` / `DC_CMD_PRINT_LAYOUT_PAPER` | 收集当前图纸布局并打开纸张打印中心。 |
| `DCD4` / `DC_CMD_PRINT_LAYOUT_PDF` | 收集当前图纸布局并打开 PDF 输出中心。 |
| `DC_CMD_DRAW_PRINT_FRAME` | 连续绘制闭合矩形打印图框。 |
| `DC_CMD_PDF_TOOL` | 打开 PDF 扫描与合并工具。 |
| `DCMB` / `DC_CMD_ELEVATION_DOOR` | 依次点击柜体内空两个对角点，预览时调整门数，左键确认后生成立面门板。 |
| `DCMBF` / `DC_CMD_PLAN_DOOR` | 依次指定柜体前沿起点和终点，预览时使用 `R` 翻转、`W/S` 或 `+/-` 调整门数，确认后生成俯视门板矩形。 |
| `DCMC` / `DC_CMD_DRAWER_FACE` | 依次指定柜体内空两个对角点；竖向用上下键、横向用左右键调整抽面数量，`+/-` 通用，确认后生成抽面、可选遮罩和所选拉手。 |
| `DCJL` / `DC_CMD_HINGE_INSERT` | 按门板上下边线单独插入铰链。 |
| `DCHJL` / `DC_CMD_HINGE_LABEL` | 框选或点选多个铰链圆，结束选择后在鼠标下方菜单批量标记内嵌、半盖或全盖。`HJL` 已被雷神工具箱占用。 |
| `DCGS` / `DC_CMD_WAINSCOT_GRILLE_DRAW` | 按参数指定左下角生成格栅板。 |
| `DCGST` / `DC_CMD_WAINSCOT_GRILLE_FILL` | 点击闭合内空后填充格栅板。 |
| `DCDB` / `DC_CMD_WAINSCOT_PERFORATED_DRAW` | 按参数指定左下角生成洞洞板。 |
| `DCDBT` / `DC_CMD_WAINSCOT_PERFORATED_FILL` | 点击闭合内空后填充洞洞板。 |
| `DCBJQ` / `DC_CMD_WAINSCOT_BACKGROUND_DRAW` | 按背景墙分排配置指定左下角生成背景墙。 |

AutoCAD 2026 默认将 `DC` 映射到原生设计中心（`ADCENTER`），本插件不覆盖该命令。

初始快捷键配置：

| 功能 | 默认 | 自定义 | 底层命令 |
| --- | --- | --- | --- |
| 打开快捷键面板 | `DCS` | `DCS` | `DC_CMD_PANEL` |
| 检查插件运行状态 | `DCJC` | `DCJC` | `DC_CMD_STATUS` |
| 批量插入图片 | `DCOLE` | `DCOLE` | `DC_CMD_IMAGE_BATCH` |
| 打开批量打印面板 | `DCDY` | `DCDY` | `DC_CMD_BATCH_PRINT` |
| 选中图框打印到纸张 | `DCD1` | `DCD1` | `DC_CMD_PRINT_PAPER` |
| 选中图框打印到 PDF | `DCD2` | `DCD2` | `DC_CMD_PRINT_PDF` |
| 当前布局打印到纸张 | `DCD3` | `DCD3` | `DC_CMD_PRINT_LAYOUT_PAPER` |
| 当前布局打印到 PDF | `DCD4` | `DCD4` | `DC_CMD_PRINT_LAYOUT_PDF` |
| 生成立面门板 | `DCMB` | `DCMB` | `DC_CMD_ELEVATION_DOOR` |
| 生成俯视门板 | `DCMBF` | `DCMBF` | `DC_CMD_PLAN_DOOR` |
| 生成抽面 | `DCMC` | `DCMC` | `DC_CMD_DRAWER_FACE` |
| 插入铰链 | `DCJL` | `DCJL` | `DC_CMD_HINGE_INSERT` |
| 标记铰链种类 | `DCHJL` | `DCHJL` | `DC_CMD_HINGE_LABEL` |

默认与自定义相同时只注册一次。即使面板快捷键被修改或禁用，也可以输入 `DC_CMD_PANEL` 恢复面板。

批量插图默认间距为 `50.0` 个图纸单位，并启用 `1000` 像素统一高度。选择图片和图中起点后，插件按实际插入宽度与设定间距从左向右排列；图片以 OLE 对象嵌入图纸，不依赖原始图片路径。

点击“添加映射”会直接新增“新功能 / NEWCMD / NEWCMD / NEWCMD”行，可继续修改功能说明、默认和自定义列，再点击“保存并应用快捷键”。系统映射不可删除；用户新增映射可以通过“删除选中”移除。CSV 导出包含功能说明、默认、自定义、禁用默认和底层命令五列。

快捷键配置保存在当前用户的：

```text
%APPDATA%\YiZhiMaoDc\ShortcutConfig.json
```

批量打印设置保存在：

```text
%APPDATA%\YiZhiMaoDc\PrintSettings.json
```

抽面拉手 DWG 放在项目的 `Modules\HandleBlocks`，构建 bundle 时会递归复制到插件的 `Contents\Modules\HandleBlocks`。建议把每个拉手 DWG 的基点设置在拉手中心；抽面设置页可从多个 DWG 中选择当前样式。
