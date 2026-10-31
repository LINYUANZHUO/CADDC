# AutoCAD 2017–2020 向下适配：官方 SDK 与引用来源

> 核验日期：2026-10-30。本文只采用 Autodesk 官方帮助/支持/APS 页面、Autodesk 所有者发布的 NuGet 页面与 NuGet V3 API，以及 Autodesk Developer Network（`ADN-DevTech`）官方 GitHub。核验过程只读取网页、JSON 元数据并对 Autodesk 下载地址发送 `HEAD` 请求；没有下载、解压或修改任何 SDK/DLL。

## 建议结论

**第一阶段最早支持到 AutoCAD 2019（`R23.0`）最合适，并以 2019 SDK 为基线同时覆盖 AutoCAD 2020（`R23.1`）。**

理由：

1. Autodesk 的[托管 .NET 兼容表](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm)明确列出：2019 与 2020 均使用 .NET Framework 4.7，2020 支持 2019 和 2020 Managed SDK；2017/2018 则是另一个 .NET Framework 4.6 边界。
2. Autodesk 的 [AutoCAD 2020 ObjectARX Release Notes](https://help.autodesk.com/cloudhelp/2020/ENU/OARX-Readme/files/GUID-4DA730AB-7A47-4663-838B-21E71438D6C8.htm)明确说 2020 对 2019 应用保持向前二进制兼容，通常无需重编译；两者还共享 Visual Studio 2017 Update 2 / `v141` 工具链。
3. CADDC 当前是 Windows x64 WPF 插件。AutoCAD 2020 起官方只提供 64 位 Windows ObjectARX；从 2019 起步可以维持一个 `Win64` 发布边界，不必为 2017/2018 的历史 32 位宿主承诺额外组合。
4. 再下探到 2017/2018 时，需要新增独立的 .NET Framework 4.6 构建、Visual Studio 2015 / `v140` 验证链，并分别针对 `R21.0`、`R22.0` SDK 编译和实机验证；这不是简单替换引用路径。

这并不表示 2017/2018 无法支持。官方 NuGet 包和历史 SDK 仍可取得，五个目标程序集也齐全；只是它们应作为第二阶段的独立兼容工作。

## 官方兼容矩阵

Release 号以 Autodesk 的 [`ACADVER`](https://help.autodesk.com/cloudhelp/2022/ENU/AutoCAD-Core/files/GUID-793238B6-F8B8-4D20-BB3A-001700AECD75.htm) 表为准；框架、Managed SDK 与工具链以 Autodesk 的[兼容表](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm)和[Visual Studio 版本说明](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-DevGuide-Managed/files/GUID-450FD531-B6F6-4BAE-9A8C-8230AAC48CB4.htm)为准。

| AutoCAD | Release series | 项目目标框架 | 官方 NuGet 精确版本（三包同版） | Windows ObjectARX SDK | 官方兼容边界 |
|---|---:|---|---|---|---|
| 2017 | `R21.0` | **.NET Framework 4.6** | [`AutoCAD.NET 21.0.2`](https://www.nuget.org/packages/AutoCAD.NET/21.0.2)、[`Core 21.0.2`](https://www.nuget.org/packages/AutoCAD.NET.Core/21.0.2)、[`Model 21.0.2`](https://www.nuget.org/packages/AutoCAD.NET.Model/21.0.2) | [`Autodesk_ObjectARX_2017_Win_64_and_32_Bit.sfx.exe`](https://download.autodesk.com/esd/objectarx/2017/Autodesk_ObjectARX_2017_Win_64_and_32_Bit.sfx.exe) | VS 2015 Update 3；Managed SDK 只列 2017 |
| 2018 | `R22.0` | **.NET Framework 4.6** | [`AutoCAD.NET 22.0.0`](https://www.nuget.org/packages/AutoCAD.NET/22.0.0)、[`Core 22.0.0`](https://www.nuget.org/packages/AutoCAD.NET.Core/22.0.0)、[`Model 22.0.0`](https://www.nuget.org/packages/AutoCAD.NET.Model/22.0.0) | [`Autodesk_ObjectARX_2018_Win_64_and_32_Bit.sfx.exe`](https://download.autodesk.com/esd/objectarx/2018/Autodesk_ObjectARX_2018_Win_64_and_32_Bit.sfx.exe) | VS 2015 Update 3；Managed SDK 只列 2018 |
| 2019 | `R23.0` | **.NET Framework 4.7** | [`AutoCAD.NET 23.0.0`](https://www.nuget.org/packages/AutoCAD.NET/23.0.0)、[`Core 23.0.0`](https://www.nuget.org/packages/AutoCAD.NET.Core/23.0.0)、[`Model 23.0.0`](https://www.nuget.org/packages/AutoCAD.NET.Model/23.0.0) | [`Autodesk_ObjectARX_2019_Win_64_and_32_Bit.sfx.exe`](https://download.autodesk.com/esd/objectarx/2019/Autodesk_ObjectARX_2019_Win_64_and_32_Bit.sfx.exe) | VS 2017 Update 2；2019 SDK 可由 2020 承载 |
| 2020 | `R23.1` | **.NET Framework 4.7** | [`AutoCAD.NET 23.1.0`](https://www.nuget.org/packages/AutoCAD.NET/23.1.0)、[`Core 23.1.0`](https://www.nuget.org/packages/AutoCAD.NET.Core/23.1.0)、[`Model 23.1.0`](https://www.nuget.org/packages/AutoCAD.NET.Model/23.1.0) | [`objectarx_for_autocad_2020_win_64_bit.sfx.exe`](https://download.autodesk.com/esd/objectarx/2020/objectarx_for_autocad_2020_win_64_bit.sfx.exe) | VS 2017 Update 2；仅 Win64 |

### 2017 的 `net45` 包标签与宿主目标框架并不矛盾

`AutoCAD.NET 21.0.2` 的 Gallery 页面和包内路径把引用程序集标为 `net45` / `lib/45`，但 Autodesk 的宿主兼容表与 Visual Studio 版本说明都要求 AutoCAD 2017 项目目标为 .NET Framework 4.6。这里应区分：

- NuGet 包的 `net45` 是“这些引用程序集能被哪些项目消费”的包资产标签；
- CADDC 为 AutoCAD 2017 生成的插件产物仍应按 Autodesk 宿主要求目标为 `net46`；
- 不应因为 2017 包能被 `net48` 项目解析，就把 `net48` 产物宣称为 AutoCAD 2017 可加载。

2017 在 NuGet 上还存在 `21.0.0`、`21.0.1`；本矩阵选用 Autodesk 发布的最后一个 21.0 修订版 `21.0.2`，且三个包必须显式锁到同一精确版本。

## 五个托管引用 DLL：官方包齐全

NuGet Gallery 的 [Autodesk 所有者页](https://www.nuget.org/profiles/Autodesk)列出 `AutoCAD.NET`、`AutoCAD.NET.Core`、`AutoCAD.NET.Model` 为 Autodesk 发布包。NuGet V3 catalog 的 `packageEntries` 不需要下载 `.nupkg` 就能核对包内文件。四个年代的拆分完全一致：

| DLL | Autodesk NuGet 包 | 2017 | 2018 | 2019 | 2020 |
|---|---|---:|---:|---:|---:|
| `AcCoreMgd.dll` | `AutoCAD.NET.Core` | 是 | 是 | 是 | 是 |
| `AcDbMgd.dll` | `AutoCAD.NET.Model` | 是 | 是 | 是 | 是 |
| `AcMgd.dll` | `AutoCAD.NET` | 是 | 是 | 是 | 是 |
| `AcWindows.dll` | `AutoCAD.NET` | 是 | 是 | 是 | 是 |
| `AdWindows.dll` | `AutoCAD.NET` | 是 | 是 | 是 | 是 |

逐版本、逐包的官方 catalog 证据：

- 2017 / `21.0.2`：[`AutoCAD.NET`](https://api.nuget.org/v3/catalog0/data/2018.11.30.06.50.31/autocad.net.21.0.2.json)、[`Core`](https://api.nuget.org/v3/catalog0/data/2018.11.30.01.09.50/autocad.net.core.21.0.2.json)、[`Model`](https://api.nuget.org/v3/catalog0/data/2018.11.30.09.20.30/autocad.net.model.21.0.2.json)
- 2018 / `22.0.0`：[`AutoCAD.NET`](https://api.nuget.org/v3/catalog0/data/2018.11.30.06.50.40/autocad.net.22.0.0.json)、[`Core`](https://api.nuget.org/v3/catalog0/data/2018.11.30.01.12.05/autocad.net.core.22.0.0.json)、[`Model`](https://api.nuget.org/v3/catalog0/data/2018.11.30.09.20.21/autocad.net.model.22.0.0.json)
- 2019 / `23.0.0`：[`AutoCAD.NET`](https://api.nuget.org/v3/catalog0/data/2018.11.30.06.50.57/autocad.net.23.0.0.json)、[`Core`](https://api.nuget.org/v3/catalog0/data/2018.11.30.01.12.05/autocad.net.core.23.0.0.json)、[`Model`](https://api.nuget.org/v3/catalog0/data/2018.11.30.09.20.21/autocad.net.model.23.0.0.json)
- 2020 / `23.1.0`：[`AutoCAD.NET`](https://api.nuget.org/v3/catalog0/data/2019.04.22.05.54.59/autocad.net.23.1.0.json)、[`Core`](https://api.nuget.org/v3/catalog0/data/2019.04.22.05.55.40/autocad.net.core.23.1.0.json)、[`Model`](https://api.nuget.org/v3/catalog0/data/2019.04.22.05.56.21/autocad.net.model.23.1.0.json)

Autodesk 的 2018、2019、2020 托管开发指南都说明 SDK 的托管 DLL 位于 `inc` 目录，建议从 SDK 而不是 AutoCAD 安装目录引用，并要求 `Copy Local=False`（[2018](https://help.autodesk.com/cloudhelp/2018/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)、[2019](https://help.autodesk.com/cloudhelp/2019/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)、[2020](https://help.autodesk.com/cloudhelp/2020/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)）。

对 CADDC 的静态盘点结果是：当前 `YiZhiMao.Dc.Plugin.csproj` 显式引用 `AcCoreMgd`、`AcDbMgd`、`AcMgd`、`AcWindows`，**没有显式引用 `AdWindows`，因此按本任务指定的“五 DLL”口径目前是 4/5**。源码确实使用了 `Autodesk.AutoCAD.Windows.PaletteSet` 和 `ColorDialog`，所以 `AcWindows` 是当前编译必需；未发现 `Autodesk.Windows` / `ComponentManager` 使用，故 `AdWindows` 目前不是编译必需。未来若使用 Autodesk 应用菜单或相关 Ribbon API，或建立统一五 DLL 输入审计，再把它加入对应版本引用集。

## ObjectARX SDK 官方入口与历史直链

当前官方入口是 [AutoCAD ObjectARX SDK downloads](https://aps.autodesk.com/developer/overview/autocad-objectarx-sdk-downloads)；[ObjectARX SDK 概览](https://aps.autodesk.com/developer/overview/objectarx-autocad-sdk)说明当前只提供最近五个版本，且从 AutoCAD 2020 起 Windows 只提供 64 位 SDK。因此 2017–2020 直链应视为仍在线的历史制品，不应假设 Autodesk 永久保留。

Autodesk Support 的[旧版下载说明](https://www.autodesk.com/br/support/technical/article/caas/sfdcarticles/sfdcarticles/PTB/Where-to-download-ObjectARX-for-AutoCAD.html)仍列出 2018、2019、2020 的直接下载入口；2017 已不在该页的当前旧版清单中，但 Autodesk 自有下载域上的历史直链仍响应。2026-10-30 仅以 `HEAD` 核验结果如下：

| SDK | HTTP | Content-Length | Last-Modified |
|---|---:|---:|---|
| [2017 Windows 64/32 位](https://download.autodesk.com/esd/objectarx/2017/Autodesk_ObjectARX_2017_Win_64_and_32_Bit.sfx.exe) | 200 | 118,119,168 | 2017-05-02 22:34:42Z |
| [2018 Windows 64/32 位](https://download.autodesk.com/esd/objectarx/2018/Autodesk_ObjectARX_2018_Win_64_and_32_Bit.sfx.exe) | 200 | 117,072,680 | 2017-11-17 20:46:00Z |
| [2019 Windows 64/32 位](https://download.autodesk.com/esd/objectarx/2019/Autodesk_ObjectARX_2019_Win_64_and_32_Bit.sfx.exe) | 200 | 112,604,304 | 2018-03-15 13:25:55Z |
| [2020 Windows 64 位](https://download.autodesk.com/esd/objectarx/2020/objectarx_for_autocad_2020_win_64_bit.sfx.exe) | 200 | 121,431,296 | 2019-03-25 19:12:52Z |

## Windows 与 CLR 边界

1. **不能复用当前 `net8.0-windows` 插件 DLL。** Autodesk 的兼容表明确把 2017/2018 放在 .NET Framework 4.6，把 2019/2020 放在 .NET Framework 4.7。现代 .NET 8 与旧版 .NET Framework CLR 是不同宿主边界；仅替换 `Ac*.dll` 引用不会让 .NET 8 产物变成旧宿主插件。
2. **2017/2018 分别使用目标年份 SDK 编译。** 两者可以共享 `net46` 源码目标，但 Autodesk 的兼容表没有声明 2017 SDK 产物可由 2018 承载，故不能在没有实机证据时合并为一个二进制。
3. **2019 基线可以覆盖 2020。** Autodesk 明确声明 2020 对 2019 ObjectARX 应用向前二进制兼容；因此只使用 2019 可用 API 时，可以构建一个 `net47` / R23.0 基线产物并在 `R23.0–R23.1` 两个宿主上验证。若使用 2020 新 API，则必须另建 R23.1 产物。
4. **CADDC 应只承诺 Win64。** Autodesk 的[64 位兼容说明](https://help.autodesk.com/cloudhelp/2020/ENU/OARX-Migration/files/GUID-EC6625F7-4645-4F4F-901A-5D606AC20ED3.htm)指出纯托管程序集本身可平台无关，但 CADDC 当前明确为 x64 WPF 插件，且 AutoCAD 2020 已没有 32 位宿主。不要因为 2017–2019 SDK 同时含 32 位文件就把现有产物标成 Win32 兼容。

## `PackageContents.xml` 的准确写法

Autodesk 的 [`RuntimeRequirements` 参考](https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-Customization/files/GUID-1591CA01-EF87-48CD-952B-772FE26037F1.htm)规定：`SeriesMin` / `SeriesMax` 接受 `R24` 或 `R24.1` 这样的 release 号；省略一端会形成开放范围。因此 CADDC 若为每年提供独立二进制，应使用精确闭区间：

```xml
<Components Description="AutoCAD 2017">
  <RuntimeRequirements OS="Win64" Platform="AutoCAD*"
                       SeriesMin="R21.0" SeriesMax="R21.0" />
  <ComponentEntry AppName="YiZhiMaoDc_R21_0"
                  ModuleName="./Contents/R21.0/YiZhiMao.Dc.Plugin.dll"
                  LoadOnAutoCADStartup="True" />
</Components>

<Components Description="AutoCAD 2018">
  <RuntimeRequirements OS="Win64" Platform="AutoCAD*"
                       SeriesMin="R22.0" SeriesMax="R22.0" />
  <ComponentEntry AppName="YiZhiMaoDc_R22_0"
                  ModuleName="./Contents/R22.0/YiZhiMao.Dc.Plugin.dll"
                  LoadOnAutoCADStartup="True" />
</Components>

<Components Description="AutoCAD 2019">
  <RuntimeRequirements OS="Win64" Platform="AutoCAD*"
                       SeriesMin="R23.0" SeriesMax="R23.0" />
  <ComponentEntry AppName="YiZhiMaoDc_R23_0"
                  ModuleName="./Contents/R23.0/YiZhiMao.Dc.Plugin.dll"
                  LoadOnAutoCADStartup="True" />
</Components>

<Components Description="AutoCAD 2020">
  <RuntimeRequirements OS="Win64" Platform="AutoCAD*"
                       SeriesMin="R23.1" SeriesMax="R23.1" />
  <ComponentEntry AppName="YiZhiMaoDc_R23_1"
                  ModuleName="./Contents/R23.1/YiZhiMao.Dc.Plugin.dll"
                  LoadOnAutoCADStartup="True" />
</Components>
```

若实际发布的是已经在 2019、2020 都验证过的同一份 **2019 SDK 基线** DLL，可以合并为：

```xml
<RuntimeRequirements OS="Win64" Platform="AutoCAD*"
                     SeriesMin="R23.0" SeriesMax="R23.1" />
```

注意：

- `SeriesMin="2017"`、`SeriesMin="21.0.2"` 都不正确；这里使用 AutoCAD release series，而不是产品年份或 NuGet 包版本。
- `Platform="AutoCAD*"` 已表示全部 AutoCAD-based products；现有 `AutoCAD|AutoCAD*` 写法语义重复。若只允许纯 AutoCAD，使用 `Platform="AutoCAD"`。
- 不要用一个宽泛的 `R21.0–R23.1` 范围指向未经每代宿主验证的同一 DLL。manifest 只控制加载选择，不会修复 CLR/API 不兼容。

## 许可与再分发边界

这部分是工程合规边界，不代替法律意见；历史版本最终以对应 SDK/NuGet 包内随附的许可文本为准。

1. Autodesk 当前的 [ObjectARX SDK License](https://aps.autodesk.com/developer/overview/autocad-objectarx-sdk-licensing)授予有限、非独占的使用和复制权。它允许为基于 AutoCAD 平台的应用开发而复制 SDK，但要求副本保留协议及版权/权利声明，并明确排除 AutoCAD LT、DWG TrueConvert、DWG TrueView。该网页当前标题只覆盖较新的 2024–2027 SDK，不能反向替代历史包内协议。
2. NuGet 包的发布者身份与再分发许可是两件事。四组三包均由 [Autodesk NuGet 所有者](https://www.nuget.org/profiles/Autodesk)发布；但 2018/2019 catalog 缺少明确 license metadata，不能把“`requireLicenseAcceptance=false` / 无 license URL”理解为可以自由再分发 Autodesk DLL。
3. CADDC 的安全边界应是：SDK/NuGet 只作为本地或 CI 的**编译输入**；五个 Autodesk DLL 均 `Private=false` / 排除 runtime 资产，不提交 Git、不复制到 CADDC bundle，不随插件发布。运行时由目标 AutoCAD 安装提供。Autodesk 自己的托管开发指南也要求 `Copy Local=False`，因为这些 DLL 已由产品提供，复制会导致意外结果。
4. [`ADN-DevTech/AutoCAD-Net-Wizards`](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards) 是官方 ADN 构建辅助仓库，其 [MIT LICENSE](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/master/LICENSE)只覆盖向导源码与文档，不覆盖 Autodesk SDK/DLL。官方仓库的 [2019 release](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/releases/tag/v2019)确认 .NET 4.7 / Visual Studio 2017 边界；[`ForAutoCAD2020/build.md`](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2020/build.md)可作为旧向导构建资料，但 GitHub 不应被当作 SDK 二进制镜像。

## 对 CADDC 后续实现的最小约束

- 第一阶段新增 `net47` / R23.0 引用集，使用 2019 SDK 或三包精确 `23.0.0`，产物在 AutoCAD 2019 和 2020 分别实测；验证通过后才把 manifest 范围写成 `R23.0–R23.1`。
- 第二阶段再新增 `net46` 的 R21.0 与 R22.0 两套引用集，分别精确使用 `21.0.2`、`22.0.0`，不要把两代合并为未证明的同一 DLL。
- 每套构建都应检查输出目录和最终 bundle 不含 `AcCoreMgd.dll`、`AcDbMgd.dll`、`AcMgd.dll`、`AcWindows.dll`、`AdWindows.dll`。
- 静态编译成功不等于宿主兼容；至少要分别记录目标 AutoCAD 年份、`ACADVER`、`NETLOAD`/autoload 结果、命令入口和 WPF 窗口打开结果。
