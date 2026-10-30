## Handoff Checkpoint

**更新时间**: 2026-10-30
**当前目标**: 逐批完成项目全面检查、缺陷修复与本机开发版部署
**当前阶段**: 第二批工具箱参数统一持久化已完成并部署；待 AutoCAD 重启验证跨进程恢复
**完成度**: 96%

### 已完成

- 新增 `%APPDATA%\YiZhiMaoDc\ToolboxSettings.json`，统一保存门板、抽面、五金、墙板、板件、图层显示和批量图片参数。
- 面板与直接命令共享同一运行时；实时预览只更新内存，显式保存或执行才写盘。
- 损坏或非法配置安全回退默认值，保存使用临时文件替换。
- 自动化测试 127/127 通过；Release 构建 0 错误；Core/Plugin 漏洞扫描无已知漏洞。
- 本批部署备份：`artifacts/deployment-backups/20261030-092647-toolbox-settings`。

- 将批量打印图框识别从横线两两全扫描改为容差量化索引，消除重复图框；5000 个图框基准由约 2.9 秒降至约 24.9 毫秒。
- 为 `System.Security.Cryptography.Pkcs` 显式锁定已修复的 `8.0.2`，Core 与 Plugin 漏洞扫描均无已知易受攻击包。
- 修复快捷键面板卸载后状态清理定时器仍继续运行的问题。
- 新增图框识别去重/容差索引、安全依赖与定时器生命周期回归测试；自动化测试 115/115 通过。
- Release 与启用最新 .NET 分析器的构建均为 0 错误；仅保留 AutoCAD 托管程序集引入的两类既有 MSB3277 冲突警告。
- 已部署 Plugin/Core DLL/PDB 与安全版 Pkcs DLL 到本机 bundle，并逐项通过 SHA-256 校验。
- 部署前备份：`artifacts/deployment-backups/20261027-project-audit`。未制作安装包，未覆盖 Modules、模板和用户配置。

### 待实机确认

- 修改门板、板件或图层参数并保存，重启 AutoCAD 后确认参数仍保留。
- 分别从面板和直接命令执行同一功能，确认读取值一致。
- 使用 W/S 或加减号临时调整门数后退出，不应覆盖显式保存的初始参数。

- 在 AutoCAD 中依次执行 `DC_CMD_STATUS`、`DCS`。
- 使用 D1/D2 对大量散线图框进行识别，确认数量正确、无重复且交互流畅。
- 关闭并重新打开工具箱面板，确认状态提示与定时清理正常。
- 对 PDF 合并/输出做一次基础冒烟测试。

### 后续优化优先级

1. 建立 AcadCoreConsole + 固定 DWG 样本的打印/图框自动冒烟测试。
2. 按功能模块拆分超大的 `ShortcutPanelView.xaml`、code-behind 和单文件控制台测试，但必须先补 UI 状态回归再拆分。
3. 将 `dotnet list package --vulnerable --include-transitive` 加入发布门禁。
4. 单独调查 AutoCAD 2026 引用导致的 Microsoft.VisualBasic/System.Drawing 版本警告，不直接屏蔽警告。

### 恢复入口

- 测试：`dotnet run --project tests\YiZhiMao.Dc.Core.Tests\YiZhiMao.Dc.Core.Tests.csproj -c Release --no-restore`
- 构建：`dotnet build CADDC.sln -c Release --no-restore -v:minimal`
- 性能：在测试命令末尾追加 `-- --benchmark-line-rectangles`
- 关键文件：`src/YiZhiMao.Dc.Core/AxisAlignedRectangleDetector.cs`、`src/YiZhiMao.Dc.Plugin/Printing/FrameRecognitionService.cs`、`src/YiZhiMao.Dc.Plugin/Views/ShortcutPanelView.xaml.cs`

### 阻塞项

- 无代码阻塞；等待用户进行 AutoCAD 内运行验收。
