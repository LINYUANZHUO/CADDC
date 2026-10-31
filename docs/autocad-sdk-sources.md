# AutoCAD 2021–2023 托管 .NET 引用程序集来源

> 核验日期：2026-10-30。来源研究先核对公开网页、GitHub 源码/元数据和 HTTP 响应头；随后已通过项目获取脚本下载 Autodesk 官方 NuGet 包，完成 NuGet 仓库签名、固定包 SHA-256、固定 DLL SHA-256 和 Autodesk Authenticode 签名验证。Autodesk DLL 未提交到仓库，也不会进入插件发布包。

## 结论

AutoCAD 2021、2022、2023 与内部版本的精确对应关系是 `R24.0`、`R24.1`、`R24.2`；Autodesk 的[兼容性表](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Customization/files/GUID-C21B8F00-C7DE-4E44-8006-D5DC99199F31.htm)还说明：2022 可运行以 2021 SDK 为基线的应用，2023 可运行以 2021/2022 SDK 为基线的应用。为了避免意外使用新 API，CADDC 的逐版本构建仍应使用目标年份自己的引用程序集。

推荐的获取顺序如下：

1. **完整 ObjectARX SDK**：从 Autodesk 自有域名取得目标年份的 Windows SDK，在本机接受包内许可后，只从 SDK 的 `inc` 目录解析引用。Autodesk 文档明确建议优先引用 SDK 中的 DLL，而不是 AutoCAD 安装目录中的 DLL（[2021](https://help.autodesk.com/cloudhelp/2021/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)、[2022](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)、[2023](https://help.autodesk.com/cloudhelp/2023/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)）。
2. **Autodesk 官方 NuGet 包**：用于可重复的编译引用时，必须精确锁定 `AutoCAD.NET`、`AutoCAD.NET.Core`、`AutoCAD.NET.Model` 三个包。NuGet Gallery 将包所有者标为 Autodesk（[Autodesk 所有者页](https://www.nuget.org/profiles/Autodesk)），NuGet 的第一方 catalog 可逐文件证明五个目标 DLL 都在这组三包中。
3. **官方 GitHub 向导**：只把它当作项目模板/构建辅助，不把它当作 SDK DLL 镜像。[`ADN-DevTech/AutoCAD-Net-Wizards`](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards) 的源码使用 MIT 许可（[LICENSE](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/master/LICENSE)），但 MIT 许可只覆盖向导源码，不覆盖 Autodesk SDK/DLL。

无论采用 SDK 还是 NuGet，都不应把 `AcCoreMgd.dll`、`AcDbMgd.dll`、`AcMgd.dll`、`AcWindows.dll`、`AdWindows.dll` 复制到插件发布目录或提交到 Git；Autodesk 的官方教程要求这些引用设置 `Copy Local = False`（[AutoCAD 2022 Autodesk University 讲义，第 43–44 页](https://static.au-uw2-stg.autodesk.com/Class_Handout_SD500025_ClassHandout-SD500025-Ambrosius-AU2021.pdf)）。

## 精确版本与官方获取地址

| 目标 AutoCAD | Release | 完整 Windows ObjectARX SDK | 官方 NuGet 精确版本 | 目标框架 |
|---|---:|---|---|---|
| 2021 | `R24.0` | [`objectarx_for_autocad_2021_win_64bit_dlm.sfx.exe`](https://download.autodesk.com/esd/objectarx/2021/objectarx_for_autocad_2021_win_64bit_dlm.sfx.exe) | [`AutoCAD.NET 24.0.0`](https://www.nuget.org/packages/AutoCAD.NET/24.0.0)、[`Core 24.0.0`](https://www.nuget.org/packages/AutoCAD.NET.Core/24.0.0)、[`Model 24.0.0`](https://www.nuget.org/packages/AutoCAD.NET.Model/24.0.0) | `net47`，可由 `net48` 项目引用 |
| 2022 | `R24.1` | [`objectarx_for_autocad_2022_win_64bit_dlm.sfx.exe`](https://download.autodesk.com/esd/objectarx/2022/objectarx_for_autocad_2022_win_64bit_dlm.sfx.exe) | [`AutoCAD.NET 24.1.51000`](https://www.nuget.org/packages/AutoCAD.NET/24.1.51000)、[`Core 24.1.51000`](https://www.nuget.org/packages/AutoCAD.NET.Core/24.1.51000)、[`Model 24.1.51000`](https://www.nuget.org/packages/AutoCAD.NET.Model/24.1.51000) | `net47`，可由 `net48` 项目引用 |
| 2023 | `R24.2` | [`ObjectARX_for_AutoCAD_2023_Win_64bit_dlm.sfx.exe`](https://damassets.autodesk.net/content/dam/autodesk/files/objectarx/ObjectARX_for_AutoCAD_2023_Win_64bit_dlm.sfx.exe) | [`AutoCAD.NET 24.2.0`](https://www.nuget.org/packages/AutoCAD.NET/24.2.0)、[`Core 24.2.0`](https://www.nuget.org/packages/AutoCAD.NET.Core/24.2.0)、[`Model 24.2.0`](https://www.nuget.org/packages/AutoCAD.NET.Model/24.2.0) | `net47`，可由 `net48` 项目引用 |

`24.1.51000` 是 Autodesk 实际发布的 AutoCAD 2022 包版本，不应自行改写成不存在的 `24.1.0`；三个 Gallery 页面分别将上述版本描述为 AutoCAD 2021、2022、2023 .NET API（[2021](https://www.nuget.org/packages/AutoCAD.NET/24.0.0)、[2022](https://www.nuget.org/packages/AutoCAD.NET/24.1.51000)、[2023](https://www.nuget.org/packages/AutoCAD.NET/24.2.0)）。

截至核验日，三个 SDK URL 都由 Autodesk 域名返回 HTTP 200；只做了 `HEAD` 请求：

| SDK | Content-Length | Last-Modified |
|---|---:|---|
| 2021 | 119,806,368 | 2020-03-26 14:11:48 GMT |
| 2022 | 120,237,280 | 2021-03-19 20:19:33 GMT |
| 2023 | 119,914,192 | 2022-03-30 14:26:26 GMT |

Autodesk 当前的 [ObjectARX SDK 下载页](https://aps.autodesk.com/developer/overview/autocad-objectarx-sdk-downloads)已经不再展示这三个旧版本，因此这些直链应视为“仍在线的历史官方制品”，不是长期可用性承诺。AutoCAD 2023 发布时，Autodesk Developer Blog 也把 ObjectARX SDK 和 2023 .NET Wizard 列为该版本的官方开发资源（[AutoCAD 2023 Developer Update](https://blog.autodesk.io/autocad-2023-developer-update/)）。

## 五个 DLL 的覆盖情况

### ObjectARX SDK

三个年份的官方托管开发指南都说明 SDK 安装后的 .NET DLL 位于 SDK `inc` 目录，并建议从这里建立引用（[2021](https://help.autodesk.com/cloudhelp/2021/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)、[2022](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)、[2023](https://help.autodesk.com/cloudhelp/2023/ENU/OARX-DevGuide-Managed/files/GUID-8657D153-0120-4881-A3C8-E00ED139E0D3.htm)）。各年份 Managed Reference 把 `AcWindows.dll` 列为 `Autodesk.AutoCAD.Windows` 的承载程序集（[2021](https://help.autodesk.com/cloudhelp/2021/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_Windows.html)、[2022](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_Windows.html)、[2023](https://help.autodesk.com/cloudhelp/2023/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_Windows.html)）；各年份开发指南还明确说 `AdWindows.dll` 可从 AutoCAD 安装目录或 ObjectARX SDK 获得（[2021](https://help.autodesk.com/cloudhelp/2021/ENU/OARX-DevGuide-Managed/files/GUID-CB7D7DC2-C8C1-4EF9-A638-C4C6184BFC85.htm)、[2022](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-DevGuide-Managed/files/GUID-CB7D7DC2-C8C1-4EF9-A638-C4C6184BFC85.htm)、[2023](https://help.autodesk.com/cloudhelp/2023/ENU/OARX-DevGuide-Managed/files/GUID-CB7D7DC2-C8C1-4EF9-A638-C4C6184BFC85.htm)）。据此，三个完整 Windows SDK 均覆盖所需五个 DLL。

### Autodesk NuGet

NuGet 的第一方 catalog 不需要下载 `.nupkg` 就能列出包内文件。三个年份的结构一致：

| DLL | 所在包 | 2021 | 2022 | 2023 |
|---|---|---:|---:|---:|
| `AcCoreMgd.dll` | `AutoCAD.NET.Core` | 是 | 是 | 是 |
| `AcDbMgd.dll` | `AutoCAD.NET.Model` | 是 | 是 | 是 |
| `AcMgd.dll` | `AutoCAD.NET` | 是 | 是 | 是 |
| `AcWindows.dll` | `AutoCAD.NET` | 是 | 是 | 是 |
| `AdWindows.dll` | `AutoCAD.NET` | 是 | 是 | 是 |

逐版本、逐包的直接 catalog 证据：

- 2021：[`AutoCAD.NET 24.0.0`](https://api.nuget.org/v3/catalog0/data/2020.05.08.03.34.29/autocad.net.24.0.0.json)、[`Core 24.0.0`](https://api.nuget.org/v3/catalog0/data/2020.05.08.03.34.42/autocad.net.core.24.0.0.json)、[`Model 24.0.0`](https://api.nuget.org/v3/catalog0/data/2020.05.08.03.35.27/autocad.net.model.24.0.0.json)。
- 2022：[`AutoCAD.NET 24.1.51000`](https://api.nuget.org/v3/catalog0/data/2021.04.14.08.00.18/autocad.net.24.1.51000.json)、[`Core 24.1.51000`](https://api.nuget.org/v3/catalog0/data/2021.04.14.07.59.59/autocad.net.core.24.1.51000.json)、[`Model 24.1.51000`](https://api.nuget.org/v3/catalog0/data/2021.04.14.07.59.59/autocad.net.model.24.1.51000.json)。
- 2023：[`AutoCAD.NET 24.2.0`](https://api.nuget.org/v3/catalog0/data/2022.04.21.09.20.46/autocad.net.24.2.0.json)、[`Core 24.2.0`](https://api.nuget.org/v3/catalog0/data/2022.04.21.09.21.11/autocad.net.core.24.2.0.json)、[`Model 24.2.0`](https://api.nuget.org/v3/catalog0/data/2022.04.21.09.21.37/autocad.net.model.24.2.0.json)。

`AutoCAD.NET` 依赖 `Core`，`Core` 再依赖 `Model`。需要特别注意：2021 的依赖区间是 `>= 24.0.0 && < 25.0.0`（[Gallery 依赖信息](https://www.nuget.org/packages/AutoCAD.NET/24.0.0)），不是严格等于 24.0.0；因此 R24.0 构建必须直接、精确锁定三包，避免 NuGet 解析成混合的 24.x 版本。2022 和 2023 的依赖元数据已经锁定到完全相同版本（[2022](https://www.nuget.org/packages/AutoCAD.NET/24.1.51000)、[2023](https://www.nuget.org/packages/AutoCAD.NET/24.2.0)），但显式锁定三包仍更容易审计。

## 许可核验

ObjectARX 不是 MIT/Apache 等通用开源 SDK。Autodesk 的[现行 ObjectARX 许可页](https://aps.autodesk.com/developer/overview/autocad-objectarx-sdk-licensing)说明，它授予有限、非独占的使用和复制许可；允许为基于 AutoCAD 平台的应用开发而复制 SDK，但要求保留协议和权利声明，并排除 AutoCAD LT、DWG TrueConvert 和 DWG TrueView。该现行网页只列出较新的版本，历史版本仍应以对应 SDK/NuGet 包随附的许可文本为准。

| 版本 | 可直接核验的许可状态 | 处理建议 |
|---|---|---|
| 2021 / `24.0.0` | NuGet 包要求接受许可并内嵌许可文件；[许可页](https://www.nuget.org/packages/AutoCAD.NET/24.0.0/license)明确覆盖 ObjectARX 2021、2020、2019 | 可在接受对应许可后作为编译引用；不要把 DLL 随插件发布 |
| 2022 / `24.1.51000` | NuGet 包要求接受许可并内嵌许可文件；[许可页](https://www.nuget.org/packages/AutoCAD.NET/24.1.51000/license)明确覆盖 ObjectARX 2022、2021、2020、2019 | 同上 |
| 2023 / `24.2.0` | Autodesk 的 [nuspec](https://api.nuget.org/v3-flatcontainer/autocad.net/24.2.0/autocad.net.nuspec)标明 `owners=Autodesk, Inc.`，但 Gallery 的[许可页](https://www.nuget.org/packages/AutoCAD.NET/24.2.0/license)明确显示“无许可信息”，`requireLicenseAcceptance=false` | 不能把“缺少许可元数据”解释为自由再分发；优先使用官方 2023 SDK，并以 SDK 随附许可为准 |

NuGet 包的发布者身份和 SDK 许可、GitHub 向导的 MIT 许可是三件不同的事。即使向导源码可按 MIT 使用，也不能据此复制、重新打包或提交 Autodesk DLL。

## GitHub 构建辅助仓库

### 可采用：Autodesk ADN 官方向导

[`ADN-DevTech/AutoCAD-Net-Wizards`](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards)是 Autodesk Developer Network 的 .NET 项目向导仓库。Autodesk 的一方材料直接指向它：2022 Autodesk University 讲义链接到 `ForAutoCAD2022` 分支（[讲义](https://static.au-uw2-stg.autodesk.com/Class_Handout_SD500025_ClassHandout-SD500025-Ambrosius-AU2021.pdf)），AutoCAD 2023 Developer Update 链接到 `ForAutoCAD2023` 分支（[发布说明](https://blog.autodesk.io/autocad-2023-developer-update/)），Autodesk Support 也给出 `ForAutoCAD2021` 的构建入口（[支持帖](https://forums.autodesk.com/t5/objectarx-forum/autocaddotnetwizard-failure/m-p/9814570/highlight/true)）。

三个历史分支的源码构建说明仍可直接读取：

- [`ForAutoCAD2021/build.md`](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2021/build.md)
- [`ForAutoCAD2022/build.md`](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2022/build.md)
- [`ForAutoCAD2023/build.md`](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2023/build.md)

该仓库适合复用项目模板和引用配置；它不是 Autodesk DLL 的替代下载源。仓库的[MIT 许可](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/master/LICENSE)允许使用、修改、分发向导源码，但构建时仍须从官方 SDK/NuGet 取得引用。

可直接审计的年度 C# 模板是 [2021 csproj](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2021/AutoCADNetWizardsInstaller/Output/AutoCAD%20CSharp%20plug-in/AutoCAD%20CSharp%20plug-in.csproj)、[2022 csproj](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2022/AutoCADNetWizardsInstaller/Output/AutoCAD%20CSharp%20plug-in/AutoCAD%20CSharp%20plug-in.csproj)和 [2023 csproj](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2023/AutoCADNetWizardsInstaller/Output/AutoCAD%20CSharp%20plug-in/AutoCAD%20CSharp%20plug-in.csproj)；路径解析逻辑也有独立源码（[Wizard.cs](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2023/ArxLocatorWizard2010/Wizard.cs)、[ObjectARXLocatorForm.cs](https://github.com/ADN-DevTech/AutoCAD-Net-Wizards/blob/ForAutoCAD2023/ArxLocatorWizard2010/ObjectARXLocatorForm.cs)）。仓库树未提交五个目标 Autodesk DLL，模板引用均使用 `Private=False`。

[`ADN-DevTech/ObjectARX-Wizards`](https://github.com/ADN-DevTech/ObjectARX-Wizards)是同组织的 C++ ObjectARX 向导，可作为原生 ARX 工程辅助；它不是 CADDC 托管 .NET 引用程序集的来源。AutoCAD 2023 官方开发更新同时链接了这个仓库和 .NET Wizard，清楚地区分了两类工具（[官方更新](https://blog.autodesk.io/autocad-2023-developer-update/)）。

另外两项许可证明确、且没有提交 Autodesk API DLL 的构建参考如下；它们的许可证只覆盖各自源码：

| 仓库 | 许可与适用范围 | 可以借鉴的内容 |
|---|---|---|
| [`ADN-DevTech/MgdDbg@6e664ce0`](https://github.com/ADN-DevTech/MgdDbg/tree/6e664ce0) | Autodesk ADN，MIT（[LICENSE](https://github.com/ADN-DevTech/MgdDbg/blob/6e664ce0/LICENSE)）；该提交直接面向 AutoCAD 2021/R24.0 | 官方三包拆分和精确 24.0.0 引用（[csproj](https://github.com/ADN-DevTech/MgdDbg/blob/6e664ce0/MgdDbg.csproj)、[packages.config](https://github.com/ADN-DevTech/MgdDbg/blob/6e664ce0/packages.config)）；提交树中没有 DLL/NUPKG |
| [`bimwright/dwg-mcp`](https://github.com/bimwright/dwg-mcp) | 社区项目，Apache-2.0（[LICENSE](https://github.com/bimwright/dwg-mcp/blob/master/LICENSE)）；有独立的 [2022](https://github.com/bimwright/dwg-mcp/blob/master/src/plugin-acad22/Bimwright.Dwg.Plugin.Acad22.csproj) 与 [2023](https://github.com/bimwright/dwg-mcp/blob/master/src/plugin-acad23/Bimwright.Dwg.Plugin.Acad23.csproj) 工程 | 多版本工程结构和从本机取得三个核心程序集；其 [README](https://github.com/bimwright/dwg-mcp/blob/master/README.md)明确声明不提交 Autodesk DLL。它不覆盖 `AcWindows`/`AdWindows`，不能代替本文的五 DLL 来源 |

### 不作为依赖来源：无明确许可或携带 DLL 的社区镜像

社区项目可以用来阅读构建思路，但必须同时满足“有明确开源许可”和“不提交/重分发 Autodesk DLL”才可复用源码。例如 [`gileCAD/AutoCAD-Csharp-Project-Template`](https://github.com/gileCAD/AutoCAD-Csharp-Project-Template)展示了从本机 ObjectARX SDK 路径引用 DLL 并设置 `Private=False` 的正确模式，但仓库首页当前没有显示许可证文件，因此只适合作为阅读材料，不应直接复制其源码进入 CADDC。

任何把 Autodesk DLL 直接放入 Git 仓库、Release 附件或非 Autodesk NuGet 包的项目，即使公开可下载，也不应作为 CADDC 的引用来源。本文没有识别出一个需要优先于 Autodesk SDK/NuGet 的社区 DLL 渠道。

已确认应排除的例子包括：[`Cazorlas/AutoCAD-all-in-one`](https://github.com/Cazorlas/AutoCAD-all-in-one)直接提交了 Autodesk DLL（例如 [2023 `AcCoreMgd.dll`](https://github.com/Cazorlas/AutoCAD-all-in-one/blob/main/libs/ObjectARX_Cad23/inc/AcCoreMgd.dll)）；[`CodeCavePro/autocad-sdk`](https://github.com/CodeCavePro/autocad-sdk)直接提交了 2021–2023 DLL（例如 [2023 `AcCoreMgd.dll`](https://github.com/CodeCavePro/autocad-sdk/blob/master/lib/2023/Any/AcCoreMgd.dll)），且根仓库没有清晰许可证。不要下载、引用或转提交这些二进制。

## 对 CADDC 的落地边界

1. AutoCAD 2021–2023 使用 .NET Framework 系列托管宿主；上述官方 NuGet 包目标为 `net47`。CADDC 当前的 `net8.0-windows` 构建不能仅靠更换 DLL 引用就加载到 R24.x，必须有独立的 `net48` 构建产物。
2. SDK 路径应由本机属性（例如 `AutoCADSdkDirectory`）传入，引用设置 `Private=false`；不要把绝对路径或 SDK DLL 写入仓库。
3. 使用 NuGet 时，三个包都应精确锁定同一版本；在 SDK 风格项目中排除运行时复制，例如对这些引用使用 `ExcludeAssets="runtime"`，并用构建检查确认输出目录没有五个 Autodesk DLL。
4. 插件发布包只包含 CADDC 自己的 DLL/PDB 和合法的第三方依赖；运行时由目标 AutoCAD 提供 Autodesk 程序集。
5. 2021、2022、2023 应分别保留引用来源、版本和 SHA-256 的构建记录；SHA-256 用于证明构建输入一致，不能替代 Autodesk 许可。

项目已将上述边界落实到 `packaging/fetch-autocad-reference-assemblies.ps1`：脚本只从 NuGet 官方 flat-container API 获取精确版本包，验证包的 NuGet 仓库签名以及包/DLL 固定 SHA-256，再验证具备 Authenticode 的 DLL。产物写入被 Git 忽略的本地 `lib\AutoCAD\Rxx.x`，正式 bundle 构建会拒绝携带这五个 Autodesk 运行时 DLL。

## 最终选择

- **来源与许可证据最完整的路径**：使用对应年份的 Autodesk ObjectARX SDK，接受随包许可，从 `inc` 引用五个 DLL，`Private=false`。
- **最便于 CI 的路径**：使用 Autodesk 所有者发布的三个精确 NuGet 包；2021 必须显式钉死三包，2023 因 Gallery 缺少许可元数据，采用前应以官方 SDK 内许可作为法律依据。
- **GitHub 的角色**：使用 Autodesk ADN 的 MIT 向导源码辅助生成/维护工程；不从 GitHub 获取 Autodesk DLL，不使用来源不明的 DLL 镜像。
