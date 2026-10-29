# 一只猫dc

面向 AutoCAD 2026 的 CAD 工具箱插件，使用 C# 和 .NET 8 开发。

## 当前状态

- 已建立可运行的 AutoCAD 插件骨架，并由用户在 AutoCAD 2026 内验证加载成功。
- 已建立 Autodesk ApplicationPlugins `.bundle` 打包结构。
- 已提供最小加载检查命令 `DC_CMD_STATUS`。
- 已实现第一版可停靠工具箱：左侧一级导航显示已实现的“快捷键”“插入图片”“批量打印”“门板抽面”和“五金件”，可拖动分隔条调整宽度，较长功能名自动换行。
- “快捷键”模块提供“重命名快捷键”和“CAD命令大全”两个标签页。
- 重命名页支持搜索，并可编辑功能说明、默认快捷键、自定义快捷键和禁用默认状态；只有底层命令保持只读。
- 已实现“添加映射”“删除选中”“导出 CSV”：新增行默认使用“新功能 / NEWCMD / NEWCMD / NEWCMD”；系统映射受保护，仅用户新增映射可以确认后删除；导出结果为 UTF-8 `.csv` 文件。
- CAD 命令大全内置 7 类 98 条常用命令，支持按快捷键、完整命令、功能说明和分类搜索。
- “插入图片”模块支持一次选择多张 JPG/JPEG/PNG/BMP/GIF 图片、指定图中起点并向右依次嵌入 OLE；可设置图片间距，并可按像素高度等比统一尺寸。
- “批量打印”模块提供 D1-D4 选框/布局出纸与 PDF 工作流、图框绘制、图框识别设置、任务清单、批量份数/纸张、预览和逐项删除。
- 独立 PDF 工具支持扫描文件夹、显示页数、全选/反选/清空，并按列表顺序合并所选 PDF。
- “门板抽面 → 立面门板”支持按柜体内空左下角、右上角动态预览并生成门板；自动铰链按柜内高度确定数量和基础位置，再把上、下盖板延伸值分别补入距顶、距底偏移，并按柜内水平层板避让。铰链参数、单独插入和类型标记入口已集中到同一页面；类型文字默认高度和距铰链圆间距均为 `35`，文字向对应门板内部展开。
- “门板抽面 → 俯视门板”使用独立参数：门板厚度默认 `18`、离柜体前沿默认 `2`、门缝默认 `3`。默认“门缝固定、门板均分”，常用门缝提供 `0.5–5` 的半毫米档位，同时允许自由输入并严格保留输入值；可设置起点/终点盖量，并只读显示最近一次实际门缝。依次指定柜体前沿起点和终点后，命令自动约束为水平或垂直方向；鼠标所在侧决定门板生成侧，`R` 翻转，`W/S` 或 `+/-` 调整门数，左键确认后仅生成关闭状态的门板矩形。
- “墙板”模块已提供格栅板、洞洞板和背景墙。背景墙支持 1–5 排从下往上配置、等分或稳定随机宽度、可选俯视/侧视图；上下排默认无缝衔接，按需可使用“缝隙”值保留垂直排缝；俯视图默认只生成最底排，也可切换为全部排。启用卡槽后，俯视大板保持与立面板相同的宽度和板缝，卡槽以每条板缝中心为中心覆盖该缝（`25×5`），每排最后一块板不生成悬空卡槽。
- 重命名表已移除“分类”列；两张表的列宽都可拖动调整，但不能小于当前表头和内容所需宽度，面板变窄时改用横向滚动显示完整内容。
- 当前仍是早期可用版本，不是功能完善的完整工具箱。

## 已知限制

- 本机尚未安装 Autodesk ObjectARX SDK 的编译期引用程序集；当前直接引用 AutoCAD 2026 安装目录中的运行时程序集。
- 构建成功，但 MSBuild 会报告 `Microsoft.VisualBasic` 与 `System.Drawing` 的 `MSB3277` 版本冲突警告。
- 新版工具箱已通过编译、核心逻辑和静态布局契约测试；运行时交互仍待用户在 AutoCAD 内验收。

## 项目约定

- 产品显示名称：`一只猫dc`
- 内部程序集前缀：`YiZhiMao.Dc`
- 底层 AutoCAD 命令前缀：`DC_CMD_`
- 首期支持范围：AutoCAD 2026，Windows x64，.NET 8
- 原雷神工具箱仅作为功能和兼容性研究样本，不复制其账号授权、品牌或受保护实现。

## 构建

```powershell
dotnet build .\CADDC.sln -c Debug
```

构建完成后，开发用 bundle 位于：

```text
artifacts\YiZhiMaoDc.bundle
```

如果 AutoCAD 安装目录不同：

```powershell
dotnet build .\CADDC.sln -c Debug /p:AutoCADDirectory="D:\Autodesk\AutoCAD 2026"
```

## 当前命令

| 命令 | 用途 |
| --- | --- |
| `DCS` | 打开“一只猫dc”可停靠面板的默认快捷键。 |
| `DC_CMD_PANEL` | 打开面板的底层命令。 |
| `DC_CMD_STATUS` | 输出插件名称和版本，用于验证程序集已经被 AutoCAD 正确加载。 |
| `OLE` | 批量插入图片的默认快捷键。 |
| `DC_CMD_IMAGE_BATCH` | 批量插入图片的底层命令。 |
| `DY` / `DC_CMD_BATCH_PRINT` | 打开批量打印模块。 |
| `D1` / `DC_CMD_PRINT_PAPER` | 识别选中的图框并打开纸张打印中心。 |
| `D2` / `DC_CMD_PRINT_PDF` | 识别选中的图框并打开 PDF 输出中心。 |
| `D3` / `DC_CMD_PRINT_LAYOUT_PAPER` | 收集全部图纸布局并打开纸张打印中心。 |
| `D4` / `DC_CMD_PRINT_LAYOUT_PDF` | 收集全部图纸布局并打开 PDF 输出中心。 |
| `DC_CMD_DRAW_PRINT_FRAME` | 连续绘制闭合矩形打印图框。 |
| `DC_CMD_PDF_TOOL` | 打开 PDF 扫描与合并工具。 |
| `DCMB` / `DC_CMD_ELEVATION_DOOR` | 依次点击柜体内空两个对角点，预览时调整门数，左键确认后生成立面门板。 |
| `DCMBF` / `DC_CMD_PLAN_DOOR` | 依次指定柜体前沿起点和终点，预览时使用 `R` 翻转、`W/S` 或 `+/-` 调整门数，确认后生成俯视门板矩形。 |
| `DCMC` / `DC_CMD_DRAWER_FACE` | 依次指定柜体内空两个对角点；竖向用上下键、横向用左右键调整抽面数量，`+/-` 通用，确认后生成抽面、可选遮罩和所选拉手。 |
| `JL` / `DC_CMD_HINGE_INSERT` | 按门板上下边线单独插入铰链。 |
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
| 检查插件运行状态 | `JC` | `JC` | `DC_CMD_STATUS` |
| 批量插入图片 | `OLE` | `OLE` | `DC_CMD_IMAGE_BATCH` |
| 打开批量打印面板 | `DY` | `DY` | `DC_CMD_BATCH_PRINT` |
| 选中图框打印到纸张 | `D1` | `D1` | `DC_CMD_PRINT_PAPER` |
| 选中图框打印到 PDF | `D2` | `D2` | `DC_CMD_PRINT_PDF` |
| 全部布局打印到纸张 | `D3` | `D3` | `DC_CMD_PRINT_LAYOUT_PAPER` |
| 全部布局打印到 PDF | `D4` | `D4` | `DC_CMD_PRINT_LAYOUT_PDF` |
| 生成立面门板 | `DCMB` | `DCMB` | `DC_CMD_ELEVATION_DOOR` |
| 生成俯视门板 | `DCMBF` | `DCMBF` | `DC_CMD_PLAN_DOOR` |
| 生成抽面 | `DCMC` | `DCMC` | `DC_CMD_DRAWER_FACE` |
| 插入铰链 | `JL` | `JL` | `DC_CMD_HINGE_INSERT` |
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
