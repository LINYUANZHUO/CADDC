# 工具箱第一版计划

## 状态

快捷键模块已完成实现和部署；批量插入图片模块已完成代码、自动化测试和构建，等待同步安装并在 AutoCAD 内完成运行验收。

## 验收口径

1. 默认快捷键 `DCS` 与底层命令 `DC_CMD_PANEL` 打开同一个 AutoCAD 可停靠面板，`DC` 仍打开原生设计中心。
2. 面板支持搜索、编辑自定义快捷键、禁用默认快捷键、重复/非法别名校验、保存并立即加载。
3. 配置保存为 JSON，生成的 LSP 仅调用已注册的 `DC_CMD_` 命令。
4. 核心配置与 LSP 生成逻辑有自动化测试，解决方案可以编译。
5. 左侧一级导航只显示已经实现的“快捷键”，宽度可拖动且长名称自动换行；选中项保留默认深色文字。右侧包含“重命名快捷键”和“CAD命令大全”。
6. 重命名表不显示“分类”列；表格行分隔线和底层命令文字为灰色。表头列宽可拖动，但不能缩到文字显示不完整；可用宽度不足时使用横向滚动。
7. 功能说明、默认、自定义和禁用默认状态可编辑，只有底层命令不可编辑；编辑结果保存后持久化并应用。
8. 点击添加映射直接创建“新功能 / NEWCMD / NEWCMD / NEWCMD”，不依赖当前选择；系统映射不可删除，用户映射须确认后删除。
9. 可将当前全部映射导出为 UTF-8 `.csv` 文件，包含功能说明、默认、自定义、禁用默认和底层命令。
10. 表格行被选中或失去焦点时，普通文字始终保持默认深色，自定义快捷键保持橙色，底层命令保持灰色；显示态和编辑态文字均垂直居中。
11. 左侧新增“插入图片”入口；支持多选 JPG/JPEG/PNG/BMP/GIF，指定起点后以 OLE 从左向右依次嵌入。
12. 图片间距默认 50.0 图纸单位；统一像素高度默认启用且为 1000，缩放保持宽高比；每张图片按实际插入宽度加间距计算下一位置。
13. 单张图片失败不阻断后续图片，命令结束时恢复图层与相关系统变量，并报告成功/失败数量。

## 架构决策

- 选择：纯 .NET 核心库负责配置、校验和 LSP 生成；AutoCAD 插件项目负责 WPF 面板和运行时加载。
- 反选：全部放入插件项目虽然文件更少，但测试进程需要加载 AutoCAD 运行时程序集，无法形成稳定的自动化测试闭环。
- 快捷键注册：为每张图纸生成文档内 `c:别名` 命令；加载新配置前将该文档旧 `c:别名` 设为 `nil`，避免多文档间的 Visual LISP 命令所有权冲突。
- 反选：`vlax-add-cmd` 注册的是进程级命令，但其回调会绑定加载脚本的文档上下文；在多图纸重复加载后会触发 `Visual LISP command document mismatch`。
- 面板入口：`DCS` 作为可配置的默认/自定义快捷键显示在列表中；`DC_CMD_PANEL` 始终保留为恢复入口；`DC` 保留给 AutoCAD 原生设计中心。
- 初始配置：`DC_CMD_PANEL` 的默认/自定义快捷键均为 `DCS`；`DC_CMD_STATUS` 的默认/自定义快捷键均为 `JC`。
- 高亮规范：快捷键文字、表格选中背景、文本选择态和主操作色使用 `RGB(239,94,37)`（`#EF5E25`）；左侧功能选中项使用浅橙背景和默认深色文字，不切换为白色。
- 信息架构：左侧为一级功能模块，只有功能实现后才新增入口；快捷键模块内部用顶部标签切换重命名与命令大全。
- 批量插图：公开快捷键为`OLE`，底层命令为`DC_CMD_IMAGE_BATCH`；参数只在当前插件会话内保留，不新增配置文件。
- OLE路径：图片先按EXIF方向纠正和可选统一高度处理，再通过增强型图元文件剪贴板交给AutoCAD `PASTECLIP` 创建嵌入对象；按创建对象的实际范围排列。
- 状态保护：导入期间临时使用可写的0层，并暂设`CMDECHO`、`OLEFRAME`、`OLEQUALITY`；结束后恢复原值。
- 溢出策略：DataGrid 列允许手动拖动，并按表头及当前内容动态设置最小宽度；左侧功能区可拖动且长名称换行。可用宽度不足时显示横向滚动条，不截断文字。
- 新增映射：用户映射使用固定只读底层命令`NEWCMD`作为初始值；功能说明、默认和自定义可编辑，至少保留一个可用快捷键。
- CSV 导出：核心库生成带 UTF-8 BOM 的 CSV，不引入第三方依赖；字段按 CSV 规则转义并通过临时文件原子替换目标文件。
- 配置迁移：当前 Schema 为 4；Schema 1–3 首次升级时沿用代码默认说明与默认快捷键，保留原自定义值；Schema 4 开始持久化可编辑的说明和默认快捷键。
- 撤销条件：未来需要兼容不支持扩展 AutoLISP 的 CAD 平台时，重新评估 CUI/PGP 或宿主原生命令注册方案。

## 依赖与影响面

- 项目知识图谱脚本不识别当前 C# 小型工程，返回空图；实际影响面以 `rg` 检查为准。
- 受影响入口：`PluginApplication`、`ShortcutPanelView`、快捷键命令目录、插件项目文件、README。
- 新增模块：`ImageBatchOptions`、`ImageBatchCommands`、插入图片面板；图片处理使用微软`System.Drawing.Common 8.0.0`。

## 验证路径

1. 运行核心自动化测试。
2. 构建 `CADDC.sln` 并检查错误数。
3. 检查 bundle 内插件 DLL、核心 DLL、PDB 和清单。
4. 本轮不启动 AutoCAD；运行时面板显示与命令加载留给用户在现有 AutoCAD 会话中验证。

## 验证结果

- 核心、配置、CSV、图片参数与布局契约自动化测试：20/20 通过。
- `dotnet build .\CADDC.sln -c Debug --no-restore`：0 错误；保留既有 2 条 `MSB3277` 版本冲突警告。
- `dotnet format .\CADDC.sln --verify-no-changes --no-restore`：通过。
- 开发用 bundle 已生成到 `artifacts\YiZhiMaoDc.bundle`。
- 批量插图产物已同步到AutoCAD插件安装目录，清单、插件/核心DLL与PDB共5个文件均与开发bundle的SHA-256一致。
- 待运行验证：多格式/多尺寸图片选择、起点拾取、统一高度、水平间距、OLE嵌入与失败续跑。

## 批量打印与 PDF 工具（本轮）

### 范围与验收

1. 左侧新增“批量打印”，包含 D1 选框打印到纸张、D2 选框打印到 PDF、D3 当前布局打印到纸张、D4 当前布局打印到 PDF、绘制打印图框和 PDF 工具。
2. 所有底层入口使用 `DC_CMD_` 前缀；主入口默认快捷键 `DY`，D1-D4 保留截图中的默认快捷键。
3. 图框识别支持闭合矩形多段线、由四条直线组成的矩形和块参照外框；可选择忽略空图框，并在打印前去重、去嵌套和按从上到下、从左到右排序。
4. 打印设置包含透明度、线宽、输出后打开目录、合并后打开 PDF、PDF 输出模式、三类分辨率、图框识别选项、设备、样式、边距和逐份打印。
5. PDF 工具支持扫描来源目录、选择/反选/清空、显示页数、选择输出目录并按列表顺序合并所选 PDF。
6. 自动验证覆盖设置持久化、图框清理排序、PDF 扫描与合并页数；AutoCAD 打印 API 仅做静态构建与部署验证，实际出纸/PDF 由用户在 CAD 内验收。

### 实现边界

- PDF 合并只拼接页面，不改写页面内容、方向或尺寸。
- 批量打印默认使用 `DWG To PDF.pc3` 与 `monochrome.ctb`；具体设备、介质和样式以当前 AutoCAD 枚举结果为准。
- 本轮不启动 AutoCAD、不打包安装程序；完成后仅同步开发 bundle 的 DLL/PDB/依赖。
- D1 只使用实体纸张设备；Windows Print Spooler 被禁用或没有实体打印机时必须明确提示，不能回退到 PDF 设备。
- 单项预览与全部预览固定使用 `DWG To PDF.pc3` 生成临时合并 PDF 并自动打开；D1 的 Print Spooler 与实体打印机检查只在真正开始出纸时执行，不阻断预览。
- `PDF 输出模式`下拉框文字须垂直居中；D3/D4 在模型空间的自动图框识别需先确认重复尺寸容差与孤立大矩形阈值，再实施全模型扫描。

### 当前验证结果

- 核心与静态界面契约测试：26/26 通过；其中 PDF 测试实际创建 1 页和 2 页样本并验证合并结果为 3 页。
- `dotnet build .\CADDC.sln -c Debug --no-restore`：0 错误；保留既有 2 类 `MSB3277` 引用版本警告。
- `dotnet format .\CADDC.sln --verify-no-changes --no-restore`：通过。
- 开发 bundle 已包含插件/核心 DLL、PDB、PDFsharp 及其运行依赖。
- 已同步到已安装 bundle；部署时 `CAD_PROCESS_COUNT=0`，17 个文件 SHA-256 全部一致。

## 墙板模块：格栅板 DCGS/DCGST 与洞洞板 DCDB/DCDBT（当前阶段）

### 范围与验收

1. 左侧“墙板”入口显示已实现的“格栅板”和“洞洞板”页。
2. 面板提供竖向/横向、平面图、侧视图、总宽高、底板厚、条厚、条宽、条间距和填充图案参数。
3. `DCGS` 调用底层 `DC_CMD_WAINSCOT_GRILLE_DRAW`，按面板尺寸从用户指定的左下角生成格栅板。
4. `DCGST` 调用底层 `DC_CMD_WAINSCOT_GRILLE_FILL`，点击闭合内空后按其外包宽高生成格栅板。
5. 正视图、可选平面图和可选侧视图使用同一套纯 Core 布局计算；AutoCAD 层只负责选点、闭合边界和落图。
6. `DCDB` 按板宽高、深度、内偏移、孔径和横纵孔数生成洞洞板；`DCDBT` 点击闭合内空后自动读取宽高。
7. 洞洞板默认附带 1 个孔径、横纵距边和横纵首个孔距代表标尺；文字高度为当前标注样式的一半，箭头为六分之一，尺寸界线超出量为五分之一，保留可见起点偏移，孔径标尺朝右上 45 度。

### 依赖与影响面

- 项目知识图谱脚本对当前 C# 路径查询返回空子图；按降级规则，以 `rg` 复核为准。
- Core 新增格栅、洞洞板参数和布局计算，插件新增墙板运行时设置与命令。
- 入口影响：`ShortcutPanelView.xaml/.xaml.cs`、`ToolboxPalette.cs`、`PluginApplication.cs`、快捷键目录和核心测试。
- 不新增第三方依赖，不复制参考插件程序集；仅按已确认的交互和几何规则独立实现。

### 验证路径

1. 先增加参数校验、竖横排列、辅助视图和命令/UI 契约测试，并确认旧实现无法通过。
2. 完成实现后运行全部核心测试、XAML XML 检查、Release 构建。
3. 本轮只备份并部署本机 DLL/PDB，不打安装包、不主动启动 AutoCAD。

### 验证结果

- 格栅参数、竖横排列、辅助视图和 UI/命令契约回归：77/77 通过。
- 本轮 C# 文件限定格式检查通过；XAML XML 解析和 Release BAML 编译通过。
- `dotnet build CADDC.sln -c Release --no-restore`：0 错误；保留既有 Microsoft.VisualBasic/System.Drawing 两类 MSB3277 警告。
- 已部署四个 Core/Plugin DLL/PDB 到本机 bundle，备份位于 `artifacts/deployment-backups/20261026-161936-wall-panel-grille`，源/目标 SHA-256 全部一致。
- 洞洞板参数、孔阵列、代表标尺、辅助视图和 UI/命令契约回归加入全套测试，当前 83/83 通过。
- 全部非实心格栅填充比例统一为 `10/6`；洞洞板已部署 `DCDB/DCDBT`，备份位于 `artifacts/deployment-backups/20261026-172403-wall-perforated-panel`，四个 DLL/PDB 源/目标 SHA-256 全部一致。
- 未主动启动 AutoCAD；DCGS/DCGST 图案比例与 DCDB/DCDBT 实际落图等待手工烟雾测试。
- 洞洞板标尺版已部署，备份位于 `artifacts/deployment-backups/20261026-180240-perforated-dimensions`，四个 DLL/PDB 源/目标 SHA-256 全部一致。
- 洞洞板孔径标尺已改为朝左上 45 度，全部标尺箭头改为当前样式的六分之一；84/84 回归通过，部署备份位于 `artifacts/deployment-backups/20261026-183700-perforated-dimension-angle-arrows`，四个 DLL/PDB 源/目标 SHA-256 全部一致。
- 洞洞板文字已调整为当前样式的四分之一，尺寸界线超出量为五分之一，并显示起点偏移；85/85 回归通过，部署备份位于 `artifacts/deployment-backups/20261026-184500-perforated-dimension-refinement`，四个 DLL/PDB 源/目标 SHA-256 全部一致。
- 洞洞板孔径标尺已镜像为右上 45 度，文字恢复为当前样式的一半；85/85 回归通过，部署备份位于 `artifacts/deployment-backups/20261026-185500-perforated-diameter-mirror-text`，四个 DLL/PDB 源/目标 SHA-256 全部一致。
- 背景墙已实现 `DCBJQ/DC_CMD_WAINSCOT_BACKGROUND_DRAW`：支持 1–5 排从下到上、每排列数与高度、等分或稳定随机宽度与俯视/侧视图；启用卡槽后，俯视大板严格复用立面板的宽度和板缝，卡槽中心对准立面板缝中心并覆盖该缝（`25×5`），每排最后一块板不生成悬空卡槽。排号自动标为底/中/顶，输入列上方显示“列数 / 高度”；89/89 回归、限定格式检查和 Release 构建通过，待本轮部署后由用户在 AutoCAD 实机验收。
- 背景墙俯视修正版已部署：精确 AutoCAD 进程数为 `0`，四个 Core/Plugin DLL/PDB 已替换，备份位于 `artifacts/deployment-backups/20261026-2008-background-wall-plan-slots`，源/目标 SHA-256 全部一致；未主动启动 AutoCAD，待用户手工运行 `DCBJQ` 验收。
- 背景墙板缝卡槽修正版已部署：精确 AutoCAD 进程数为 `0`，四个 Core/Plugin DLL/PDB 已替换，备份位于 `artifacts/deployment-backups/20261026-2056-background-wall-seam-slots`，源/目标 SHA-256 全部一致；未主动启动 AutoCAD，待用户手工运行 `DCBJQ` 验收。
- 背景墙俯视板缝对齐修正版已部署：俯视大板复用立面板的宽度和板缝，卡槽仅按板缝中心生成；精确 AutoCAD 进程数为 `0`，四个 Core/Plugin DLL/PDB 已替换，源/目标 SHA-256 全部一致，替换前备份位于 `artifacts/deployment-backups/20261026-212620-background-wall-plan-seam-alignment`。首次部署尝试的备份目录因 PowerShell 参数不兼容未创建，但最终部署已在覆盖前完成备份；未主动启动 AutoCAD，待用户手工运行 `DCBJQ` 验收。
- 背景墙视图选项已细分：垂直排间默认不留缝，启用后复用“缝隙”数值；俯视图默认只生成最底排，启用“全部排均生成俯视图”后逐排生成。90/90 回归、限定格式检查及 Release/XAML 重编译通过；四个 DLL/PDB 已部署，备份位于 `artifacts/deployment-backups/20261026-2148-background-wall-view-options`，源/目标 SHA-256 全部一致。
- 俯视门板 `DCMBF/DC_CMD_PLAN_DOOR` 已实现并独立保存运行期参数：厚度默认 `18`、门缝默认 `3`，支持计算模式、起点/终点盖量和最近实际门缝显示；两点前沿自动约束水平/垂直，鼠标侧决定方向，`R` 翻转，`W/S` 或 `+/-` 调门数，确认后只生成关闭矩形。93/93 回归、限定格式检查及 Release/XAML 重编译通过；四个 DLL/PDB 已部署，备份位于 `artifacts/deployment-backups/20261026-223857-plan-door-panel`，源/目标 SHA-256 全部一致。
- 俯视门板排产参数已调整：默认使用“门缝固定、门板均分”，常用 `0.5–5` 半毫米门缝可选但不限制自由输入；新增“离柜体前沿”参数，默认 `2`，门板内边先沿生成侧偏移该距离，再按厚度向外生成。回归覆盖自定义 `3.2` 门缝保持不变以及 `2 + 18 = 20` 的外边位置；93/93 回归、限定格式检查及 Release/XAML 重编译通过。部署前精确路径 AutoCAD 进程为 `0`，四个 DLL/PDB 已替换，备份位于 `artifacts/deployment-backups/20261026-232856-plan-door-fixed-gap-clearance`，源/目标 SHA-256 全部一致。
- 抽面首版已实现 `DCMC/DC_CMD_DRAWER_FACE`：默认竖向 2 块、固定 3mm 缝，支持横向、自由缝隙、四向盖值、Wipeout 背景遮罩（默认关闭）与 `Modules/HandleBlocks` 多拉手选择；竖向使用上下键、横向使用左右键，`+/-` 通用，预览阶段只画轻量矩形，确认后才落图。96/96 回归、限定变更文件格式检查和 Release/XAML 构建通过。部署时发现精确路径 AutoCAD 残留 PID 12200，按用户既有授权强制结束并确认进程数为 0；四个 DLL/PDB 已部署且 SHA-256 全部一致，备份位于 `artifacts/deployment-backups/20261027-0018-drawer-face`，`HandleBlocks` 已同步到安装 bundle，待手工烟雾测试。

### 首轮运行缺陷修复

- 修复 AutoCAD 2026 下布局字典运行时返回 `DictionaryEntry`、原 LINQ 强转为 `DBDictionaryEntry` 导致 D3/D4 初始化失败的问题；现在兼容两种枚举项类型。
- 打印设备和纸张先按设备实际支持列表解析，再一次性写入正式 `PlotSettings`；不再让设备与旧纸张处于不一致的中间状态。
- 打印样式按当前图纸的 CTB/STB 模式过滤，并在设备、纸张、样式、打印区域、参数校验和输出阶段保留准确失败阶段。
- 修复后自动验证为 26/26，通过格式检查并编译 0 错误；用户关闭 CAD 后已同步修复版 bundle，17 个文件 SHA-256 全部一致。
