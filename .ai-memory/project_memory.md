# 一只猫dc 项目记忆

## Project Convention: 产品与命令命名

**类型**: 命名规范
**适用范围**: 全项目

### 规范内容

- 产品显示名称固定为“一只猫dc”。
- 内部程序集和命名空间使用 `YiZhiMao.Dc` 前缀。
- 代码实现并注册的底层 AutoCAD 命令使用 `DC_CMD_` 前缀；快捷键编辑器新增用户行的固定占位底层命令为 `NEWCMD`。
- 工具箱面板默认快捷键使用 `DCS`，恢复入口使用 `DC_CMD_PANEL`；`DC` 是 AutoCAD 原生设计中心别名，不得覆盖。
- 每完成一项功能，必须说明当前属于骨架、可编译、可使用还是已完善，并建议下一步。
- 遇到会改变功能方向、兼容范围或交互方式的不确定项，先询问用户。
- 部署时若用户已关闭 AutoCAD，但仍有路径为 `D:\Autodesk\AutoCAD 2026\acad.exe` 的无窗口残留进程，核对路径后直接强制结束，不再重复询问；不得结束其他路径的同名进程。

### 示例

**正确**: `DC_CMD_STATUS`

**错误**: `STATUS`

### 发现来源

2026-10-25 用户约定。

## Decision Record: 首期技术基线

**日期**: 2026-10-25
**问题**: 新插件首期采用什么运行基线。

### 选项分析

| 选项 | 优势 | 劣势 | 复杂度 |
| --- | --- | --- | --- |
| AutoCAD 2026 + .NET 8 | 与当前已安装环境一致，可直接编译验证 | 暂不覆盖旧版 CAD | 低 |
| 同时覆盖旧版 AutoCAD | 覆盖面更广 | 需要多目标框架和多套验证环境 | 高 |

### 决策

**选择**: 首期仅支持 AutoCAD 2026 + .NET 8。
**理由**: 先建立可验证的最小开发闭环。
**Trade-offs**: 旧版 AutoCAD、中望 CAD、浩辰 CAD 暂不支持。

### 影响范围

- `src/YiZhiMao.Dc.Plugin`
- `packaging/YiZhiMaoDc.bundle`

### 撤销条件

用户明确要求增加其他 CAD 品牌或版本，并具备对应运行验证环境。

## Decision Record: 面板默认入口使用 DCS

**日期**: 2026-10-25
**问题**: 工具箱面板公开命令是否可以使用 `DC`。

### 决策

**选择**: 使用 `DCS` 作为工具箱面板的默认/自定义快捷键，保留 `DC_CMD_PANEL` 为底层恢复命令；`DC_CMD_STATUS` 的默认/自定义快捷键均为 `JC`。
**理由**: 本机 AutoCAD 2026 的 `acad.pgp` 明确包含 `DC, *ADCENTER`，`DC` 已用于原生设计中心；`DCS` 未被该别名文件占用。

### 影响范围

- `src/YiZhiMao.Dc.Plugin/PanelCommands.cs`
- `src/YiZhiMao.Dc.Plugin/StatusCommands.cs`
- 面板提示和使用文档

### 撤销条件

目标 CAD 环境确认占用 `DCS`，届时在快捷键面板修改或禁用该默认值，底层命令仍保持 `DC_CMD_PANEL`。

## Project Convention: 选择与快捷键高亮色

**类型**: UI规范
**适用范围**: 一只猫dc界面

### 规范内容

- 默认选择高亮色固定为 `RGB(239,94,37)`，十六进制为 `#EF5E25`。
- 快捷键自定义文字、表格选择状态、输入框文字选择和主操作按钮使用同一强调色。
- 左侧功能导航的选中项使用浅橙背景与橙色边框，文字保持默认深色，不切换成白色。
- 快捷键表选中行只改变背景；普通文字始终保持深色，自定义快捷键保持橙色，底层命令保持灰色，聚焦与失焦均不得切换文字颜色。
- 左侧功能区和表头列宽允许用户拖动；表格列的最小宽度必须覆盖当前表头和内容，不能因拖动造成文字截断。

### 发现来源

2026-10-25 用户约定。

## Decision Record: 快捷键面板与动态别名机制

**日期**: 2026-10-25
**问题**: 如何让快捷键可校验、可持久化并在当前AutoCAD会话中修改或禁用后立即替换。

### 选项分析

| 选项 | 优势 | 劣势 | 复杂度 |
| --- | --- | --- | --- |
| JSON + 文档内 `c:XXX` AutoLISP函数 | 不跨图纸绑定文档上下文，可即时加载，核心逻辑可测试 | 每张图纸都需要加载生成的LSP | 低 |
| `vlax-add-cmd`/`vlax-remove-cmd` 动态注册 | 可注册进程级别名 | 回调绑定加载脚本的文档，多图纸切换会发生文档错配 | 中 |
| 修改PGP/CUI | 接近AutoCAD原生配置 | 影响用户全局配置，刷新与回滚更复杂 | 高 |

### 决策

**选择**: JSON + 文档内 `c:XXX` 命令LSP；配置保存在 `%APPDATA%\YiZhiMaoDc`。加载新配置前，将当前文档旧别名对应的 `c:` 符号设为 `nil`，再定义新别名。
**理由**: 满足重复校验、用户级持久化和当前图纸立即替换旧映射；不修改用户的AutoCAD全局PGP/CUI。
**Trade-offs**: 首期仅面向AutoCAD 2026 Windows；每个图纸文档都需加载生成的LSP。

### 影响范围

- `src/YiZhiMao.Dc.Core`
- `src/YiZhiMao.Dc.Plugin/Shortcuts`
- `src/YiZhiMao.Dc.Plugin/Views`

### 撤销条件

后续需要支持不具备AutoLISP的CAD平台，或需要让未加载LSP的文档也立即获得别名时，再评估PGP/CUI或纯.NET别名方案。

## Decision Record: 可编辑命令映射与CSV导出

**日期**: 2026-10-25
**问题**: 如何实现添加、删除和导出，同时保护代码内置功能目录。

### 决策

**选择**: 功能说明、默认、自定义和禁用默认状态均可编辑，只有底层命令只读。新增映射固定初始化为“新功能 / NEWCMD / NEWCMD / NEWCMD”，系统映射不可删除，用户映射可确认删除。配置Schema升级为4；导出为带UTF-8 BOM的CSV。
**理由**: 面板承担完整快捷键映射编辑职责；只锁定底层命令可避免别名指向被误改，同时CSV更便于直接检查和交换。
**Trade-offs**: 新增行的底层命令固定为`NEWCMD`，不能在面板内改指向；CSV仅导出，不支持导入。

### 影响范围

- `src/YiZhiMao.Dc.Core`
- `src/YiZhiMao.Dc.Plugin/Views`
- `tests/YiZhiMao.Dc.Core.Tests`

### 撤销条件

后续明确需要导入CSV或修改用户行底层命令时，再扩展导入冲突与命令指向规则。

## Decision Record: 批量图片以OLE嵌入并水平排列

**日期**: 2026-10-25
**问题**: 如何实现与参考工具箱交互一致的多图片插入，同时避免图纸依赖外部图片路径。

### 决策

**选择**: 新增左侧“插入图片”模块，默认快捷键`OLE`、底层命令`DC_CMD_IMAGE_BATCH`。图片按EXIF方向纠正，可选等比缩放到统一像素高度，经增强型图元文件剪贴板和`PASTECLIP`嵌入为OLE对象，再按实际对象宽度加设定间距向右排列。
**理由**: OLE嵌入满足图纸自包含；实际对象范围比仅按像素换算更可靠；逐张处理允许坏图失败后继续。
**Trade-offs**: 插入过程会占用系统剪贴板；大图转换为EMF有15 MB上限；首版只提供单行水平排列，参数只保留在当前插件会话。

### 影响范围

- `src/YiZhiMao.Dc.Core/ImageBatchOptions.cs`
- `src/YiZhiMao.Dc.Plugin/ImageBatchCommands.cs`
- `src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.*`

### 撤销条件

用户明确要求外部参照图片、二维矩阵排版或跨会话持久化图片参数时，重新设计插入和配置模型。

## Decision Record: 打印预览固定生成临时PDF

**日期**: 2026-10-25
**问题**: 如何让D1-D4的单项和全部预览显示真实打印结果，同时避免D1的实体打印服务状态阻断预览。

### 选项分析

| 选项 | 优势 | 劣势 | 复杂度 |
| --- | --- | --- | --- |
| 仅缩放CAD视图 | 实现简单 | 不能验证纸张、样式和打印窗口的真实输出 | 低 |
| 使用实体设备执行预览 | 与D1设备一致 | 依赖Print Spooler和实体打印机，可能产生误出纸 | 中 |
| 固定生成临时PDF并自动打开 | 可查看真实输出，不依赖实体打印服务 | 会在系统临时目录保留预览文件 | 中 |

### 决策

**选择**: 单项和全部预览固定使用`DWG To PDF.pc3`，以合并模式生成唯一临时PDF并自动打开；D1的Print Spooler和实体设备检查延后到实际出纸按钮。
**理由**: 预览应验证最终输出内容而非仅定位图框，并且不能产生实体打印副作用。
**Trade-offs**: 预览使用PDF设备的可用纸张；若当前任务纸张名称不兼容，则按该设备和当前布局支持的介质回退。

### 影响范围

- `src/YiZhiMao.Dc.Plugin/Printing/CadPlotService.cs`
- `src/YiZhiMao.Dc.Plugin/Views/BatchPrintWindow.xaml.cs`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 撤销条件

用户明确要求调用AutoCAD原生打印预览界面，或要求预览严格模拟某一实体打印机不可打印区域时，再拆分为设备专属预览方案。

## Decision Record: 门板预览合并刷新与层板按面板重叠避让

**日期**: 2026-10-25
**问题**: 快速调整门板数量时预览卡顿，且盖板延伸可能使铰链圆心越过层板线端点，导致层板避让漏判。

### 决策

**选择**: 门数热键只在状态实际变化时安排一次Dispatcher渲染优先级刷新；层板是否影响某扇门按层板线与最终门板的水平正交叠判定，不再按铰链圆心X判定。
**理由**: 原同步刷新随按键重复消息线性放大重绘成本；圆心点包含关系不是“层板穿过门板”的业务语义，盖板延伸会直接破坏该假设。
**Trade-offs**: 快速连续按键可能在同一渲染帧内合并为一次画面更新，但最终门板数量不会丢失；层板只要与门板有正交叠就会参与避让。

### 影响范围

- `src/YiZhiMao.Dc.Core/HardwareLayoutCalculator.cs`
- `src/YiZhiMao.Dc.Plugin/DoorPanelCommands.cs`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 撤销条件

若后续业务要求仅按铰链安装侧的局部层板参与避让，应引入明确的侧边作用范围参数，而不是恢复圆心点包含判定。

## Decision Record: 门板预览与正式生成分阶段执行

**日期**: 2026-10-26
**问题**: 门板数量调整阶段绘制完整门板和开门线会造成重复瞬态绘制，影响交互流畅度。

### 选项分析

| 选项 | 优势 | 劣势 | 复杂度 |
| --- | --- | --- | --- |
| 预览完整门板、开门线和铰链 | 所见即最终结果 | 鼠标移动和数量调整时重复绘制成本高 | 中 |
| 预览轻量线框，确认后生成完整构件 | 调整数量更流畅，正式结果不变 | 预览阶段不显示开门方向和铰链 | 低 |

### 决策

**选择**: 两点范围确定后，DrawJig 只绘制一个总外框和门缝分隔线；用户用 W/S 或 +/- 调整数量并左键确认后，才生成正式门板、开门虚线和铰链。
**理由**: 预览阶段只需确认门板数量和分格，不需要提前承担完整构件生成成本。
**Trade-offs**: 开门方向和铰链位置只能在确认后查看；需要修改时重新执行命令。

### 影响范围

- `src/YiZhiMao.Dc.Plugin/DoorPanelCommands.cs`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 撤销条件

若后续明确要求预览开门方向或铰链，应使用独立的低成本符号或缓存图元，不能恢复每帧完整构件生成。

## Decision Record: 门板命令默认采用两点对角取框

**日期**: 2026-10-26
**问题**: 门板预览虽已轻量化，但命令入口仍会在第一次点击后自动识别内空并立即进入预览，与用户要求的左下角、右上角两次点击流程不一致。

### 决策

**选择**: `DCMB` 默认依次获取柜体内空的第一角点和对角点，只有第二点确认有效范围后才进入门数预览。
**理由**: 两点范围由用户明确控制，避免第一次点击即显示预览，也与现场操作口径一致。
**Trade-offs**: 每个柜格固定多一次点击，但范围更可控；原 `DoorOpeningDetector` 保留为几何能力和测试，不再接入默认命令入口。

### 影响范围

- `src/YiZhiMao.Dc.Plugin/DoorPanelCommands.cs`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`
- `README.md`

### 撤销条件

只有用户以后明确要求恢复“一点自动识别”并指定其入口方式时，才重新接入该路径；不得再次替代默认两点流程。

## Decision Record: 铰链避让兼容宽薄实体层板

**日期**: 2026-10-26
**问题**: 门板命令的层板收集器只读取顶层直线和轻量多段线的水平段；层板以块参照或其他带厚度实体存在时不会进入避让计算，导致铰链仍与层板重叠。

### 决策

**选择**: 保留原水平线识别，并读取实体几何包围盒；宽度大于 100 mm、厚度 10–50 mm 的横向薄实体按包围盒中心线作为层板位置。
**理由**: 该口径覆盖常见 18/20/25 mm 层板以及对应块参照，同时延续雷神门板模块按宽薄实体识别层板的行为。
**Trade-offs**: 旋转板件或包含整柜内容的大块参照不按该规则识别；普通高实体与短实体会被过滤，降低误判。

### 影响范围

- `src/YiZhiMao.Dc.Core/HardwareLayoutCalculator.cs`
- `src/YiZhiMao.Dc.Plugin/DoorPanelCommands.cs`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 撤销条件

若现场层板厚度超出 10–50 mm 或需要识别旋转/整柜嵌套块，应新增明确的层板识别参数或递归块几何规则，不扩大当前固定阈值。

## Decision Record: 门板开门线与铰链标尺统一以铰链侧定向

**日期**: 2026-10-26
**问题**: 门板生成后开门虚线向自由边汇合，铰链标尺也向门外偏移；两者都与现场识图和加工量尺方向相反。

### 选项分析

| 选项 | 优势 | 劣势 | 复杂度 |
| --- | --- | --- | --- |
| 分别按屏幕左右固定方向修正 | 代码直观 | 单门换边或多门交替铰链时会再次出错 | 低 |
| 统一以每扇门的铰链侧计算方向 | 左右门、单门换边和多门规则一致 | 很窄门板内标尺可用空间更小 | 低 |

### 决策

**选择**: 开门虚线从自由边上下角向铰链侧中点汇合；铰链标尺从铰链边向门板内部展开。
**理由**: 两种图形都表达铰链侧关系，使用同一业务基准可避免屏幕左右符号混淆，并满足加工时在门内读取尺寸的要求。
**Trade-offs**: 窄门板内的多层标尺可能更拥挤；如现场出现重叠，应调整标尺间距或文字高度，而不能把标尺重新移到门外。

### 影响范围

- `src/YiZhiMao.Dc.Plugin/DoorPanelCommands.cs`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 撤销条件

只有用户明确要求某种门型采用不同的开门符号或外侧标尺规范时，才针对该门型新增独立规则；默认立面门板继续以铰链侧为统一基准。

## Decision Record: JL 与自动门板共享铰链尺寸，HJL 独立为圆多选标记

**日期**: 2026-10-26
**问题**: JL 虽读取运行时参数，但默认值仍为 80/25/25 且标尺画在门外；HJL 错误复用门板上下边选择，无法批量标记已生成铰链。

### 决策

**选择**: `HardwareOptions` 作为 JL 和自动门板的唯一铰链尺寸参数源，默认为上下定位 / 60 / 80 / 50，标尺统一向门内布置。HJL 仅选择 `DC_铰链` 图层的圆，支持左键框选或点选多个，右键或空格结束后选择内嵌/半盖/全盖，并在每个圆心写标记。
**理由**: 一套参数避免面板与快捷命令行为漂移；圆+图层过滤与用户工作流和雷神 HJL 参考行为一致。
**Trade-offs**: HJL 不标记其他图层的普通圆；铰链类型文字高度暂固定为 10 mm，未增加额外配置项。

### 影响范围

- `src/YiZhiMao.Dc.Core/HardwareOptions.cs`
- `src/YiZhiMao.Dc.Plugin/HardwareCommands.cs`
- `src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.xaml`
- `src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.xaml.cs`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 撤销条件

只有用户明确要求 HJL 接受非本插件图层的铰链圆，或要求更多铰链类型/可调文字样式时，再扩展过滤与配置；不应恢复选择门板上下边的旧 HJL 流程。

## Decision Record: DCMB 层板作用范围与盖板延伸解耦，HJL 使用鼠标旁菜单

**日期**: 2026-10-26
**问题**: 自动门板最终铰链把盖板延伸后的门板外形用于层板候选判定，导致延伸值改变避让对象；HJL 类型只能在命令行选择，用户开启动态输入后仍没有鼠标旁提示。

### 选项分析

| 选项 | 优势 | 劣势 | 复杂度 |
| --- | --- | --- | --- |
| 继续复用最终门板范围并调整阈值 | 改动小 | 延伸值仍会改变层板归属，无法根治 | 低 |
| 独立传递原始柜内净空，并使用鼠标旁显式菜单 | 业务边界稳定，不依赖动态输入显示状态 | HJL 多一个小型 WPF 窗口 | 低 |

### 决策

**选择**: 门板轮廓、铰链坐标继续使用盖板延伸；层板候选固定按 DCMB 两点选择的原始柜内净空筛选。HJL 多选结束后打开当前鼠标下方的内嵌/半盖/全盖模态菜单，支持 D/X/Z、Enter 和 Esc。
**理由**: 柜体内层板归属与门板盖值是两个独立业务概念；显式菜单不依赖 AutoCAD 的 `DYNMODE`/`DYNPROMPT` 组合状态。
**Trade-offs**: 同一柜内范围里的局部层板会作为该次生成中所有门板的避让候选，优先保证不碰层板；混合 DPI 多显示器上的菜单位置仍需 AutoCAD 实机确认。

### 影响范围

- `src/YiZhiMao.Dc.Core/HardwareLayoutCalculator.cs`
- `src/YiZhiMao.Dc.Plugin/DoorPanelCommands.cs`
- `src/YiZhiMao.Dc.Plugin/HardwareCommands.cs`
- `src/YiZhiMao.Dc.Plugin/Views/HingeTypeMenuWindow.xaml*`

### 撤销条件

若以后要求局部层板只影响指定门扇，应在柜内净空中增加明确的门扇分区规则；若 AutoCAD 版本对无边框 WPF 模态窗口兼容不稳定，再改用 Autodesk 原生弹出组件，但不得恢复仅命令行关键字的交互。

## Decision Record: DCMB 铰链按柜内定位补偿延伸，HJL 文字向门内锚定

**日期**: 2026-10-26
**问题**: 只把层板候选限制在柜内仍不足以保证避让；初始铰链按延伸门板定位时，大延伸会改变铰链世界坐标。HJL 面板按钮有效，但 LISP 快捷键同步嵌套底层命令时不能稳定进入鼠标菜单；类型文字固定 10 mm 且压在圆心。

### 决策

**选择**: 自动门板铰链数量按原柜内高度计算；门板相对距顶、距底分别使用“基础值 + 上延伸”和“基础值 + 下延伸”，之后再执行柜内层板避让。HJL 快捷键使用 `vla-SendCommand` 在 LISP 返回后启动 `DC_CMD_HINGE_LABEL`。类型文字从铰链圆边向对应门板内部偏移，默认文字高度和间距均为 35，并允许在门板抽面中调整。
**理由**: 柜内构造决定铰链的加工基准，延伸只改变门板轮廓；异步命令入口与面板 `SendStringToExecute` 语义一致；左右锚点能保证文字主体位于门内且间距含义明确。
**Trade-offs**: HJL 只对弹窗型命令采用延迟发送，其他快捷键保持现有同步入口；无法找到闭合门板时退回最近门侧竖线判断方向，仍需 AutoCAD 实图验证复杂图纸。

### 影响范围

- `src/YiZhiMao.Dc.Core/HardwareLayoutCalculator.cs`
- `src/YiZhiMao.Dc.Core/HardwareOptions.cs`
- `src/YiZhiMao.Dc.Core/AutoLispShortcutGenerator.cs`
- `src/YiZhiMao.Dc.Plugin/DoorPanelCommands.cs`
- `src/YiZhiMao.Dc.Plugin/HardwareCommands.cs`
- `src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.xaml*`

### 撤销条件

若现场明确要求铰链随门板外轮廓而非柜内构造移动，才撤销延伸补偿；若更多交互式快捷键出现同类 LISP 嵌套问题，再将延迟发送从 HJL 特例推广为统一策略。

## Decision Record: 关键快捷入口使用原生命令兜底并避开雷神同名别名

**日期**: 2026-10-26
**问题**: 雷神 `LS_CustomShortcuts.lsp` 与本插件同时定义 `C:HJL`，加载顺序会改变命令归属；DCMB 仅存在于生成的 LSP 时，脚本未加载的文档无法识别快捷命令。

### 决策

**选择**: 保持 `DC_CMD_ELEVATION_DOOR` 与 `DC_CMD_HINGE_LABEL` 为稳定底层命令；额外注册 .NET 原生 `DCMB` 与 `DCHJL` 入口。默认铰链标记快捷键从 `HJL` 改为 `DCHJL`，配置 schema 升至 6，并把未修改的旧 HJL 自动迁移到 DCHJL。
**理由**: 原生命令不依赖文档内 LSP 是否加载；`DC` 前缀避免与参考插件争抢相同 `C:` 函数，同时保留快捷键面板的可配置层。
**Trade-offs**: 原 `HJL` 继续归雷神；使用本插件铰链标记必须输入 `DCHJL` 或完整底层命令。原生 DCMB/DCHJL 是固定保底入口，不随快捷键面板禁用。

### 影响范围

- `src/YiZhiMao.Dc.Core/ShortcutConfiguration.cs`
- `src/YiZhiMao.Dc.Plugin/DoorPanelCommands.cs`
- `src/YiZhiMao.Dc.Plugin/HardwareCommands.cs`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 撤销条件

只有移除雷神或确认所有目标 CAD 环境不再注册 HJL 时，才考虑恢复 HJL；关键交互命令仍应保留原生兜底，不退回纯 LSP 入口。

## Decision Record: 墙板格栅布局放在 Core，GS/GST 只负责交互与落图

**日期**: 2026-10-26
**问题**: 格栅板包含竖横排列、俯视/侧视和内空填充；若全部直接写在 AutoCAD 命令中，几何规则难以自动回归。

### 决策

**选择**: `GrilleOptions` 统一参数校验，`GrilleLayoutCalculator` 输出正视图、俯视图和侧视图矩形；`WainscotCommands` 只执行 GS 左下角选点、GST 闭合边界追踪、图层/多段线/填充落图。
**理由**: 纯几何可以在没有 AutoCAD 运行时的测试进程中验证，CAD API 部分保持最小且与现有插件分层一致。
**Trade-offs**: GST 当前按 AutoCAD `TraceBoundary` 返回的首个闭合边界外包框生成，不处理旋转内空或非矩形边界的精确裁切。

### 影响范围

- `src/YiZhiMao.Dc.Core/GrilleOptions.cs`
- `src/YiZhiMao.Dc.Core/GrilleLayoutCalculator.cs`
- `src/YiZhiMao.Dc.Plugin/WainscotCommands.cs`
- `src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.xaml*`

### 撤销条件

若现场需要旋转格栅、任意多边形裁切或逐条交互编辑，再扩展几何模型；当前矩形柜墙板不提前泛化。

## Decision Record: 墙板快捷键统一增加 DC 前缀并迁移旧默认值

**日期**: 2026-10-26
**问题**: 格栅板首版默认快捷键 GS/GST 未遵循本插件避免命令冲突的 DC 前缀约定，且非实心填充图案显示比例偏大。

### 决策

**选择**: 默认快捷键调整为 DCGS/DCGST；配置 schema 升至 7，旧配置中未修改的 GS/GST 自动迁移，用户自行修改过的快捷键保持不变；非 SOLID 的 `Hatch.PatternScale` 从 20 调整为 10。
**理由**: DC 前缀与一只猫dc现有命令规范一致并降低与其他插件冲突概率；比例减半直接对应用户确认的显示效果。
**Trade-offs**: 快捷命令依旧由自动生成的 LISP 注册，稳定底层命令保持 `DC_CMD_WAINSCOT_GRILLE_DRAW/FILL`；SOLID 没有可见图案尺度，继续使用 1。

### 影响范围

- `src/YiZhiMao.Dc.Core/ShortcutConfiguration.cs`
- `src/YiZhiMao.Dc.Plugin/WainscotCommands.cs`
- `src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.xaml`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 撤销条件

只有用户明确要求兼容无前缀旧命令时才额外增加别名；不得把稳定底层命令改成短命令。

## Project Convention: 抽面开门标记、预览热键与拉手资源

**类型**: 交互规范 / 资源打包约束
**适用范围**: `DCMC` 抽面生成、`Modules/HandleBlocks` 拉手库

### 规范内容

- 每块正式生成的抽面必须带位于面板中心、箭头朝下的三段式开门标记，并使用 `DC_开门线` 虚线图层。
- 抽面预览期间左/右键始终表示数量减一/加一并由命令接管，不能交给 AutoCAD 动态输入移动光标；竖向排列可同时使用上/下键；`+/-` 始终通用。
- `Modules/HandleBlocks` 以 DWG 为素材入口，可配同名 PNG/JPG/JPEG/BMP 预览；`.bak` 不进入 bundle，也不出现在样式列表中。

### 示例

**正确**: 横向或竖向预览按右键后数量加一，正式生成的每块抽面都有向下箭头；`牛角拉手（160）.bak` 不出现在安装目录。

**错误**: 竖向预览时左右键移动动态输入光标；只生成抽面矩形；把 AutoCAD 自动备份文件打进插件资源。

### 发现来源

2026-10-27 用户实测反馈与抽面/拉手资源回归修复。

## Project Convention: 立面与俯视门板实际门缝补偿

**类型**: 业务计算规则
**适用范围**: 立面门板 `DCMB`、俯视门板 `DCMBF`

### 规范内容

- 多扇门实际门缝统一为 `设定门缝 + (门扇数 - 1) / (门扇数 + 1)`；例如设定 3mm 时，2 扇为 3.3333mm、3 扇为 3.5mm、10 扇为 3.8182mm、100 扇为 3.9802mm。
- 门缝取整模式必须先对设定门缝按既有规则取整，再叠加门扇数补偿。
- 门板取整模式由最后一扇门吸收宽度余量，最右边界不得漂移。
- 单门没有门间缝，实际门缝固定为 0；抽面不是本规则适用对象，继续使用用户设定门缝。

### 发现来源

2026-10-27 用户提供门扇数补偿表并确认立面门板和俯视门板统一使用。

## Project Convention: 抽面开门线、门缝补偿与拉手应用（替代旧抽面规范）

**类型**: 交互规范 / 业务规则
**适用范围**: `DCMC`, `DCSDH`, `DCLAS`

### 规范内容

- 本节替代此前“三段式向下箭头”和“抽面不补偿门缝”的约定。
- 抽面开门线必须与门板开门线同型：两条虚线从上方两个角汇合到下边中心，表示抽屉向下打开。
- 抽面和立面/俯视门板统一使用 `实际门缝 = 设定门缝 + (N-1)/(N+1)`；计算模式决定门或缝如何取整，最终边界必须收口。
- DCMC 只生成抽面、开门线和可选遮罩。拉手使用 `DCSDH` 拾取当前图纸块，再用 `DCLAS` 应用到闭合门板/抽面或原位替换已有拉手块。
- 左/右键固定调整抽面数量，竖向额外支持上/下键，`+/-` 通用。

### 发现来源

2026-10-27 用户确认抽面开门线、统一门缝公式和拉手拾取应用工作流。

## Project Convention: 图层图纸安全边界

**类型**: 数据安全 / CAD 图层规则
**适用范围**: `TC`, `TCJZ`, `TCGC`, `PU`, `TCWJ`, `TCMB`, `TCHG`, `TCBZ`, `TCXS`, `TXF`

### 规范内容

- 标准图层只补齐缺失项，不覆盖同名图层的用户设置。
- 智能归层只处理明确类型：标注、文字、填充以及已有 `DC_*` 插件层别名；不得按普通线条几何猜测柜体或门板。
- 深度清理必须二次确认；快速隐藏必须保留 0 层可用，并提供显示全部恢复入口。
- TXF 调整全局虚线比例，并遍历所有非外部参照本地图块记录，修改墙体填充、护墙板及 `DC_墙板` 上的非 SOLID Hatch 比例后强制重算。

## Feature Baseline: 板件第一版

**确定日期**: 2026-10-27

- 板件模块固定使用“板件 / 单双线 / 组件”三页，不加入未要求的“分区”页。
- 第一版基础命令为 `DCCB/DCZC/DC1/DC2/DC3/DCZ2/DCZ3/DCJJ/DCJX`；高级位移、夹层、均分、背板和编辑命令分批实现。
- 板件标识比例范围为 0.1~10，单双线可分别选择生成线或闭合矩形，组件页先实现十字酒架与 45 度斜酒架。

## Feature Baseline: 板件高级操作与拆单统计

**确定日期**: 2026-10-27

- 板件高级命令固定为 `DCYC/DCABA/DCFC/DCBB/DCFG/DCHB/DCEXX/DCTRR/DCFS`，底层命令统一以 `DC_CMD_BOARD_` 开头。
- 拆分、合并、延伸和修剪第一版只对水平/竖直四边闭合矩形提供确定性编辑；旋转矩形不做猜测性处理。
- “拆单统计”是左侧独立模块，固定为部件统计、普通门板、边框门板、柜体拆单、板件统计五页，输出 Excel 可直接打开的 UTF-8 BOM CSV。
- 柜体结构板件的几何约定为：平面长边是板长、平面短边是板厚、Polyline `Thickness` 是板深；背板 `Thickness` 则是背板厚度。统计时必须按图层区分这两类语义。
- 当前板件统计只输出规格、数量和面积，不将面积除以标准板面积冒充精确开料排版。

## Known Issue: WPF 跨模块样式必须位于共同祖先资源作用域

**发现日期**: 2026-10-27
**问题类型**: Bug 模式
**严重程度**: 严重

### 现象

工具箱初始化时抛出 `XamlParseException`，提示找不到 `DoorPanelCompactGroupStyle`。

### 根因

样式定义在门板模块的 `Grid.Resources`，图层图纸等兄弟模块引用时不可见；XAML 编译成功不能证明运行期静态资源作用域正确。

### 规避方案

跨模块复用的 `StaticResource` 必须定义在 `UserControl.Resources` 或更高共同祖先；静态回归需校验资源定义范围，而不只校验资源名称存在。

### 相关文件

- `src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.xaml`
- `tests/YiZhiMao.Dc.Core.Tests/Program.cs`

### 状态

已修复，待 AutoCAD 实机确认面板正常打开。

## Project Convention: 圆盘鼠标入口

**确定日期**: 2026-10-27
**类型**: AutoCAD 交互约定
**适用范围**: 圆盘运行时、`DCYP`、工具箱圆盘设置

- 圆盘的主鼠标入口是双击中键，对应 `WM_MBUTTONDBLCLK`；只有圆盘已启用时才拦截该消息并在 AutoCAD Idle 阶段显示圆盘。
- 中键单击及按住拖动不得拦截，必须继续保留 AutoCAD 平移行为；圆盘停用时双击中键也不得拦截。
- `DCYP` 命令和面板按钮是稳定备用入口，不因鼠标入口加入而删除。
- 鼠标消息订阅必须随插件 `Initialize/Terminate` 成对注册和注销，防止重复订阅或卸载后残留。

## Project Convention: 安装升级与卸载必须保留用户数据

**确定日期**: 2026-10-31
**类型**: 安装与发布约定
**适用范围**: `packaging/Installer`、正式发布安装包

- 默认按当前 Windows 用户安装到 Autodesk 标准 `ApplicationPlugins`；所有用户安装才申请管理员权限，不支持任意安装目录。
- 升级必须先完整备份旧 bundle，再启用新版；只把 `Modules` 中用户新增或修改且新版不存在的资源恢复到活动库。与新版同名的资源以新版为准，旧文件留在完整备份并计入冲突数。
- `%APPDATA%\YiZhiMaoDc` 中的快捷键、打印和工具箱配置不得被升级覆盖。
- 安装失败必须删除半安装的新 bundle 并恢复旧 bundle；卸载失败必须恢复 bundle、当前用户数据和卸载注册信息。
- 卸载默认保留配置，并另行导出用户新增或修改的模板、LISP、五金资源；“删除当前用户数据”必须默认不勾选并二次确认。
- 安装和卸载日志固定写入 `%LOCALAPPDATA%\YiZhiMaoDc\Installer\Logs`，完成页显示路径与资源保留数量。
