## Handoff Checkpoint

**更新时间**: 2026-10-31
**当前目标**: 完成安全安装、升级、卸载与 AutoCAD 2017–2027 正式发布验证
**当前阶段**: 安装器资源保留、事务回滚、卸载选项和日志已完成；正式安装包已通过完整发布门禁，待 Windows/AutoCAD UI 人工验收
**完成度**: 98%

### 已完成

- 安装向导默认当前用户、可选所有用户/UAC，只使用 Autodesk 标准 `ApplicationPlugins` 路径；检测 AutoCAD 后支持重试、身份校验后的强制关闭或取消。
- 升级会完整备份旧 bundle，自动恢复 `Modules` 内用户新增或修改的模板、LISP、五金资源；新版同名文件优先，冲突旧文件留在备份中。
- 安装失败会删除半安装的新 bundle 并恢复旧版；卸载失败会恢复 bundle、当前用户数据和注册信息。安装/卸载均写入本地日志。
- 卸载默认保留配置和用户资源；可选且默认不勾选“删除当前用户配置和自定义资源”，并带二次确认。
- 自动化回归 137/137 通过；AutoCAD 2017–2027 十一版本矩阵及 bundle 构建均为 0 警告、0 错误；安装器内嵌 bundle 自检通过。
- 正式产物：`artifacts/YiZhiMaoDc-Setup-v0.1.0.exe`（SHA-256 `2AC61AADFE8BF211BEC3FF2018BEA97176811C374F40891E8B6B336E0829A1F1`）与 `artifacts/YiZhiMaoDc-Green-v0.1.0.zip`（SHA-256 `CA26AB0DBDB96CF5F7DD704B42C005EC718C2B0255804AFC7881D6206629007A`）。

- 新增 `%APPDATA%\YiZhiMaoDc\ToolboxSettings.json`，统一保存门板、抽面、五金、墙板、板件、图层显示和批量图片参数。
- 面板与直接命令共享同一运行时；实时预览只更新内存，显式保存或执行才写盘。
- 损坏或非法配置安全回退默认值，保存使用临时文件替换。
- 自动化测试 127/127 通过；Release 构建 0 错误；Core/Plugin 漏洞扫描无已知漏洞。
- 本批部署备份：`artifacts/deployment-backups/20261030-092647-toolbox-settings`。
- 已消除 AutoCAD 2026 引用导致的 Microsoft.VisualBasic/System.Drawing 冲突；Release 构建现为 0 警告、0 错误。
- 新增 `tests/no-autocad-reference-conflicts.ps1` 构建门禁；本批部署备份：`artifacts/deployment-backups/20261030-093626-desktop-reference-unification`。
- 新增 `tests/autocad-core-smoke.ps1`：真实加载 Release 插件，在固定 `DimTemplate.dwg` 中执行 `DC_CMD_STATUS` 与 `DC_CMD_BOARD_DRAW`，并断言生成对象为闭合四顶点多段线。
- 核心控制台模式不再初始化快捷键文件和鼠标圆盘事件；普通 AutoCAD 行为不变。门禁含 60 秒超时与进程回收，最终主机冒烟通过。
- 本批部署只替换变化的 Plugin DLL/PDB，四个 DLL/PDB SHA-256 均与 Release 一致；备份：`artifacts/deployment-backups/20261030-095709-core-console-smoke`。
- 新增仅测试使用的 `tests/YiZhiMao.Dc.HostProbe`，通过真实 AutoCAD 2026 托管宿主调用内部图框识别服务；不会加入解决方案或打包到 bundle。
- 固定图框样本同时覆盖散线框、重复闭合框、嵌套框、上下排与左右排序，主机断言最终只保留 3 个外层图框并按阅读顺序返回。
- 本批没有生产代码或二进制变化；Release 与本机 bundle 中 Core/Plugin DLL/PDB 的 SHA-256 逐项一致，因此未重复部署、未新增备份。

- 将批量打印图框识别从横线两两全扫描改为容差量化索引，消除重复图框；5000 个图框基准由约 2.9 秒降至约 24.9 毫秒。
- 为 `System.Security.Cryptography.Pkcs` 显式锁定已修复的 `8.0.2`，Core 与 Plugin 漏洞扫描均无已知易受攻击包。
- 修复快捷键面板卸载后状态清理定时器仍继续运行的问题。
- 新增图框识别去重/容差索引、安全依赖与定时器生命周期回归测试；自动化测试 115/115 通过。
- Release 与启用最新 .NET 分析器的构建均为 0 错误；仅保留 AutoCAD 托管程序集引入的两类既有 MSB3277 冲突警告。
- 已部署 Plugin/Core DLL/PDB 与安全版 Pkcs DLL 到本机 bundle，并逐项通过 SHA-256 校验。
- 部署前备份：`artifacts/deployment-backups/20261027-project-audit`。未制作安装包，未覆盖 Modules、模板和用户配置。

### 待实机确认

- 用正式安装包分别验证当前用户首次安装、覆盖升级、默认卸载保留资源，以及勾选后的当前用户数据清理；本轮未直接改动本机已安装 bundle。

- 修改门板、板件或图层参数并保存，重启 AutoCAD 后确认参数仍保留。
- 分别从面板和直接命令执行同一功能，确认读取值一致。
- 使用 W/S 或加减号临时调整门数后退出，不应覆盖显式保存的初始参数。

- 在完整 AutoCAD 中执行 `DCS`，确认 WPF 面板、鼠标圆盘和快捷键脚本正常。
- 使用 D1/D2 对大量散线图框进行识别，确认数量正确、无重复且交互流畅。
- 关闭并重新打开工具箱面板，确认状态提示与定时清理正常。
- 对 PDF 合并/输出做一次基础冒烟测试。

### 后续优化优先级

1. 在现有 Core Console 固定 DWG 门禁上补充块参照图框和空图框过滤断言；打印/PDF 仍需隔离真实设备和输出目录后再自动化。
2. 按功能模块拆分超大的 `ShortcutPanelView.xaml`、code-behind 和单文件控制台测试，但必须先补 UI 状态回归再拆分。
3. 在完整 AutoCAD 中完成 WPF 面板、鼠标圆盘、D1/D2 与 PDF 输出的人工验收。

### 恢复入口

- 测试：`dotnet run --project tests\YiZhiMao.Dc.Core.Tests\YiZhiMao.Dc.Core.Tests.csproj -c Release --no-restore`
- 构建：`dotnet build CADDC.sln -c Release --no-restore -v:minimal`
- 主机冒烟：`tests\autocad-core-smoke.ps1 -SkipBuild`
- 性能：在测试命令末尾追加 `-- --benchmark-line-rectangles`
- 关键文件：`src/YiZhiMao.Dc.Core/AxisAlignedRectangleDetector.cs`、`src/YiZhiMao.Dc.Plugin/Printing/FrameRecognitionService.cs`、`src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.xaml.cs`

### 阻塞项

- 无代码阻塞；等待用户进行 AutoCAD 内运行验收。
