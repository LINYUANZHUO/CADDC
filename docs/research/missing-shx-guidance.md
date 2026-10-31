# AutoCAD 缺失 SHX：原因、官方处理流程与 CADDC 实现边界

> 研究日期：2026-10-31
>
> 资料范围：Autodesk 官方帮助/技术支持，以及开源仓库的 README、源码和许可证。未采用论坛、博客、字体下载站或聚合文章。
> 本文是研究与设计建议，不代表已经在真实 AutoCAD 会话中完成 UI、字体外观或打印结果验证。

## 结论摘要

1. `.shx` 只是编译后形定义文件的扩展名，既可能是文字字体，也可能是独立形/符号文件；Big Font 又是为大量非 ASCII 字符设计的特殊 SHX。收到“缺少 SHX”时，第一步必须按引用角色分类，不能统一当成“缺字体”。[Autodesk：About Shape Descriptions](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-DE941DB5-7044-433C-AA68-2A9AE98A5713.htm)、[Autodesk：Use Unicode and Big Fonts (.NET)](https://help.autodesk.com/view/ACD/2026/ENU/?caas=caas%2Fdocumentation%2FCIV3D%2F2014%2FENU%2FfilesACD%2FGUID-E0A705AF-6357-4F9F-A5E3-EDAFF5D887D9-htm.html)

2. Autodesk 列出的常见原因包括：SHX 未随 DWG 传递、被移动/改名/删除、不在搜索路径中、文件损坏或不可读，以及复杂线型引用的 shape SHX 缺失。文字可能被 `simplex.shx` 替代、消失或错显；线型符号也可能缺失。[Autodesk：Missing SHX Files](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/AutoCAD-cannot-find-SHX-font.html)

3. 对文字 SHX，官方替代顺序是字体映射表、文字样式中的字体、`FONTALT`、最后提示用户；`FONTALT` 默认是 `simplex.shx`。这套机制是文字字体兜底，不是 shape/复杂线型 SHX 的通用修复器。[Autodesk：About Substitute Fonts](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-928DF015-1E04-4CC2-AF1B-0037548DFBAE.htm)、[Autodesk：FONTALT](https://help.autodesk.com/cloudhelp/2025/ENG/AutoCAD-Core/files/GUID-858A8D84-5104-4A35-9C82-2094DF6F411D.htm)

4. 最可靠的来源是原图发送方。应要求发送方使用 `ETRANSMIT` 并启用 `Include Fonts`，随后检查文件表、传递报告和最终包中是否确有目标 SHX；不能只看到“eTransmit 完成”就假定字体已经打入。[Autodesk：Modify Transmittal Setup](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-9A2F96E5-34D1-4504-A5F3-54BCD410041D.htm)、[Autodesk：Files Table](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-A329E883-840A-4584-A59A-06468DEB6137.htm)、[Autodesk：Transmittal Package File Reference](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-C32FA153-88D6-41D9-B868-0DFF59509CD2.htm)

5. 第一版 CADDC 不应根据 DWG 内的文件名静默从公网自动下载。建议只做“检测、分类、本地查找、来源说明、显式替代和 eTransmit 引导”；若以后引入联网能力，也只应连接受控字体库，并采用固定源、许可证清单、哈希/签名验证、资源限额、隔离解析和用户确认。[Autodesk：Additional SHX Fonts](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Additional-SHX-fonts.html)、[Autodesk：Error Reading Shape File](https://help.autodesk.com/view/ACADWEB/ENU/?caas=caas%2Fsfdcarticles%2Fsfdcarticles%2FError-reading-shape-file-message-when-using-an-SHX-font-in-AutoCAD.html)

## 1. 三类 SHX 必须分开处理

| 角色 | AutoCAD 中的含义 | 典型引用 | 正确处理边界 |
| --- | --- | --- | --- |
| 普通文字 SHX | 每个字符对应一个形定义的文字字体 | `TextStyleTableRecord.FileName`、TEXT/MTEXT/标注等使用的文字样式 | 可通过原字体、`FONTMAP`、`FONTALT` 或显式替代处理，但替代可能改变字宽、换行和符号外观 |
| Big Font SHX | 与普通 SHX 配对，用于包含大量非 ASCII 字符的语言字体 | `TextStyleTableRecord.BigFontFileName` | 必须作为 Big Font 单独检测和替代，不能把普通字体命中误报为 Big Font 已解决 |
| shape/符号 SHX | 独立形定义，常用于 `SHAPE` 实体或复杂线型符号 | shape 型 `TextStyleTableRecord`、`Shape.StyleId`、复杂线型段 | `FONTALT` 不能完整解决；应找出引用它的形、线型和对象，补齐原文件或修改/移除引用 |

Autodesk 明确说明：SHP 编译为 SHX；若定义文件首项描述字体，它就是字体文件，若首项描述形，它就是 shape 文件。Big Font 则是特殊的形定义文件。也就是说，扩展名和文件名都不足以判断用途。[Autodesk：About Shape Descriptions](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-DE941DB5-7044-433C-AA68-2A9AE98A5713.htm)

在 AutoCAD .NET API 中，可以用下列宿主信息完成分类，而不必自己猜二进制格式：

- `TextStyleTableRecord.FileName`、`BigFontFileName` 和 `IsShapeFile` 分别表示主字体、Big Font 和“按 shape 记录解释”。[Autodesk：TextStyleTableRecord Properties](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-__MEMBERTYPE_Properties_Autodesk_AutoCAD_DatabaseServices_TextStyleTableRecord.html)、[Autodesk：IsShapeFile](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_TextStyleTableRecord_IsShapeFile.html)
- 复杂线型的每一段可通过 `LinetypeTableRecord.ShapeStyleAt(index)` 找到对应的 shape 或文字样式记录。[Autodesk：ShapeStyleAt](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_LinetypeTableRecord_ShapeStyleAt_int.html)
- 如果审计范围包括 MTEXT 局部格式覆盖，可用 `MText.ExplodeFragments()` 按特定字体等特征拆成片段再检查。[Autodesk：MText.ExplodeFragments](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_MText_ExplodeFragments_MTextFragmentCallback.html)

## 2. 为什么打开 DWG 会提示缺少 SHX

官方故障说明覆盖了以下原因：

- 原始 SHX 没有随 DWG 传递，或后来被移动、改名、删除；
- SHX 存在，但不在 DWG 当前目录、AutoCAD Fonts 目录或有效的 Support File Search Path 中；
- 文件损坏、不可读，或被 Windows/安全软件阻止；
- 包含该字体目录的搜索路径已失效，或 AutoCAD 配置/安装损坏；
- 复杂线型引用了缺失的 shape SHX，而不是文字样式缺字体。

来源：[Autodesk：Missing SHX Files](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/AutoCAD-cannot-find-SHX-font.html)、[Autodesk：Error Reading Shape File](https://help.autodesk.com/view/ACADWEB/ENU/?caas=caas%2Fsfdcarticles%2Fsfdcarticles%2FError-reading-shape-file-message-when-using-an-SHX-font-in-AutoCAD.html)

“替代后能打开”不等于问题已经永久消失。Autodesk 说明，在缺失提示里选择替代只会让当前计算机用另一字体显示，原文字样式引用仍可保留，因此 `STYLE` 中仍可能继续显示缺失警告。要永久改变图纸，应在确认影响后修改文字样式并保存副本；要保持原始版式，应优先找回原 SHX。[Autodesk：Missing SHX Files](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/AutoCAD-cannot-find-SHX-font.html)

## 3. Autodesk 官方推荐处理流程

### 3.1 先记录缺失名称并分类

1. 在 Missing SHX Files 对话框中记录每个缺失文件名；必要时查看命令行替代记录。
2. 用 `STYLE` 检查文字样式中的普通字体和 Big Font。
3. 若 `STYLE` 无对应项，或提示来自复杂线型，使用 `PURGE > Find Non-Purgeable Items > Shapes` 查找依赖该 shape 的线型/对象。
4. 若此前选择了“不再提示”，可在 `Options > System > Hidden Messages Settings` 恢复提示。

来源：[Autodesk：Missing SHX Files](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/AutoCAD-cannot-find-SHX-font.html)、[Autodesk：Hidden Messages](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-to-enable-Dialog-prompts-that-are-not-displayed-anymore.html)

### 3.2 按 AutoCAD 自身的搜索规则找原文件

当未提供完整路径时，AutoCAD 的库搜索顺序是：当前工作目录、当前 DWG 所在目录、Support File Search Path 中列出的目录、程序安装目录。Support File Search Path 内部按列表顺序搜索，同名文件取第一个命中。[Autodesk：About File Organization](https://help.autodesk.com/view/ACD/2026/ENU/?caas=caas%2Fdocumentation%2FACDLT%2F2014%2FENU%2Ffiles%2FGUID-25616676-EFF2-4060-A06F-19112622A78B-htm.html)、[Autodesk：Files Tab](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-F95EE827-7567-44EA-9D69-E9D0D37EE13F.htm)

官方支持的常见放置方式是：

- 与 DWG 放在同一目录；
- 放入 AutoCAD 标准 Fonts 目录；
- 放入专门的公司/项目字体目录，并将该目录加入 Support File Search Path。

来源：[Autodesk：Install a New or Custom Font](https://help.autodesk.com/view/ACADWEB/ENU/?caas=caas%2Fsfdcarticles%2Fsfdcarticles%2FWhere-to-install-font-shape-files-in-AutoCAD.html)

实现侧应优先调用 AutoCAD 当前宿主的 `HostApplicationServices.FindFile()`，传入关联 `Database` 与适当 `FindFileHint`，让宿主考虑 DWG 路径和文件类型；不要在 CADDC 中复制一套容易漂移的搜索规则。[Autodesk：HostApplicationServices.FindFile](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_HostApplicationServices_FindFile_string_Database_FindFileHint.html)

注意：从 AutoCAD 2026 起，启用 Autodesk Docs 项目感知时，支持文件配置可能由项目管理员控制，普通用户不一定能修改。[Autodesk：Modify Support File Search Paths](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-LT-Customization/files/GUID-348D791E-D621-444F-B059-C0C6F97AD69F.htm)

### 3.3 向发送方索取 eTransmit 包

推荐请求语可以直接写为：

> 请在原始 AutoCAD 环境中对 DWG 执行 ETRANSMIT，在 Transmittal Setup 中勾选 Include Fonts，并确认缺失的 `.shx` 出现在 Files Table、Transmittal Report 和最终 ZIP 的 Fonts 目录中。

这样写比“请打包一下”更准确，原因是 Autodesk 文档同时存在三层语义：eTransmit 会收集图纸依赖；设置页提供显式的 `Include Fonts`；文件类型参考又将 `.shx` 列在“不是自动加入”的类型中。因此接收方必须检查实际清单和包内容，不能只依赖概括性说明。[Autodesk：Modify Transmittal Setup](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-9A2F96E5-34D1-4504-A5F3-54BCD410041D.htm)、[Autodesk：Files Table](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-A329E883-840A-4584-A59A-06468DEB6137.htm)、[Autodesk：Transmittal Package File Reference](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-C32FA153-88D6-41D9-B868-0DFF59509CD2.htm)

eTransmit 只能传递发送方已有且允许传递的文件，不能修复发送方机器上本就缺失或损坏的 SHX，也不自动授予字体再分发许可。

### 3.4 只有找不到原字体时才做文字替代

对文字 SHX，AutoCAD 的官方顺序为：

1. 字体映射文件（`FONTMAP` 指向的 `.fmp`）；
2. 文字样式中定义的字体；
3. `FONTALT`；
4. 提示用户选择替代。

`.fmp` 是普通 ASCII 文本，每行采用“原字体名（无路径和扩展名）`;` 替代文件名（含扩展名）”格式。`FONTALT` 是全局备用字体，默认 `simplex.shx`；Big Font 需要按普通字体与 Big Font 的配对语义处理。[Autodesk：About Substitute Fonts](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-928DF015-1E04-4CC2-AF1B-0037548DFBAE.htm)、[Autodesk：FONTALT](https://help.autodesk.com/cloudhelp/2025/ENG/AutoCAD-Core/files/GUID-858A8D84-5104-4A35-9C82-2094DF6F411D.htm)、[Autodesk：Specify Font for Style](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-D8D215DD-D177-4C98-85D1-034460017572.htm)

任何替代都应在 UI 和日志中明确显示“原名 → 替代名”，并提醒用户检查字宽、换行、对齐、标注、特殊符号和打印结果。Autodesk 也提醒替代字体应尽量相似，以避免文字长度和换行问题。[Autodesk：About Substitute Fonts](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-928DF015-1E04-4CC2-AF1B-0037548DFBAE.htm)

### 3.5 shape/复杂线型缺失时处理引用，而不是改 FONTALT

官方流程是：在 `PURGE > Find Non-Purgeable Items > Shapes` 中找到缺失 shape，再查看使用它的线型和对象。随后只能选择：

- 补齐原 SHX；
- 将对象改为不依赖该 shape 的线型；
- 删除相关对象后清理不再使用的线型/shape。

来源：[Autodesk：Missing SHX Files](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/AutoCAD-cannot-find-SHX-font.html)、[Autodesk：Removing Shapes and Purging References](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Erase-shapes-and-purge-references-to-unavailable-shape-SHX-files.html)

## 4. 字体版权、许可证与 eTransmit 边界

1. 解析器的开源许可证不等于字体文件的许可证。即使 CADDC 使用 MIT 解析器，也不因此获得 Autodesk 或第三方 SHX/TTF 的复制、打包或再分发权。[ezdxf：源码中的 Autodesk 字体版权说明](https://github.com/mozman/ezdxf/blob/5265e6e6d02388a95f406c26983e5c39452ad003/src/ezdxf/fonts/fonts.py)、[ezdxf：MIT License](https://github.com/mozman/ezdxf/blob/5265e6e6d02388a95f406c26983e5c39452ad003/LICENSE)

2. Autodesk 的 Unicode SHX 头包含 embedding/licensing 信息：`0` 可嵌入、`1` 不可嵌入、`2` 只读嵌入；官方明确指出，受许可保护的字体不得被修改或交换。[Autodesk：About Unicode Font Descriptions](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-D38A5A7B-1877-46B3-8120-32DA5F7430D1.htm)

3. 将其他字体编译成 SHX 不会消除原字体版权。Autodesk 明确说明，编译后的 SHX 通常继续受原字体相同的版权限制。[Autodesk：About Compiling Shape and Font Files](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-BC8EFEAC-D640-410A-8EC8-2EBB38DE6563.htm)

4. eTransmit 的 `Include Fonts` 是技术打包选项，不是许可授予。官方对 TrueType 字体特别要求确认接收方拥有相应字体；对 SHX 也仍应检查字体自身许可证和 embedding 标志。[Autodesk：Modify Transmittal Setup](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-9A2F96E5-34D1-4504-A5F3-54BCD410041D.htm)、[Autodesk：About Unicode Font Descriptions](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-D38A5A7B-1877-46B3-8120-32DA5F7430D1.htm)

## 5. 为什么不应静默自动下载 SHX

Autodesk 没有提供一个可按文件名安全解析的通用官方 SHX 仓库，并且对外部 SHX 下载站点不作推荐或背书，提示访问外部站点可能给软件或机器带来风险。[Autodesk：Additional SHX Fonts](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Additional-SHX-fonts.html)

基于官方行为和开源解析器源码，可得到以下工程判断：

- DWG 可声明任意字体文件名；按名字拼接公网 URL 会把图纸输入变成下载目标，来源和内容均不可验证。
- 同名 SHX 可能出现在多个目录，AutoCAD 取搜索顺序中的第一个；静默写入或覆盖可能改变其他图纸的显示。[Autodesk：Files Tab](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-F95EE827-7567-44EA-9D69-E9D0D37EE13F.htm)
- SHX 可能损坏、被安全软件拦截或无法读取；因此下载完成不等于可安全交给 AutoCAD。[Autodesk：Error Reading Shape File](https://help.autodesk.com/view/ACADWEB/ENU/?caas=caas%2Fsfdcarticles%2Fsfdcarticles%2FError-reading-shape-file-message-when-using-an-SHX-font-in-AutoCAD.html)
- Support File Search Path 还用于插件、定制文件、线型和填充图案，不能把通用下载目录直接加入其中。[Autodesk：Files Tab](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-F95EE827-7567-44EA-9D69-E9D0D37EE13F.htm)
- 自动下载还可能违反字体的 embedding 标志或单独许可。[Autodesk：About Unicode Font Descriptions](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-D38A5A7B-1877-46B3-8120-32DA5F7430D1.htm)

这里不把 SHX 描述成“可执行程序”，因为现有一手资料不足以支持该说法。已确认的风险是：不可信/损坏二进制输入、错误字形与版式、许可证违规，以及通用支持目录中的同名文件遮蔽。

若未来业务确实要求联网，最低安全门槛应是：

1. 只允许固定 HTTPS 域名和服务器端清单，不允许根据 DWG 内容拼任意 URL；
2. 清单必须包含规范 basename、SHA-256、大小、许可证、来源和版本；
3. 先下载到隔离暂存区，限制文件大小、记录数、递归深度、解析时间和内存；
4. 杀毒/信誉检查与结构解析通过后，仍需用户确认；
5. 使用内容寻址、不可变缓存，绝不按同名静默覆盖；
6. 安装到专用、只读的受管字体目录，不把通用下载目录加入 Support File Search Path；
7. 为每次解析、替代、安装记录原名、来源、哈希、许可证和用户选择。

以上七项是基于官方搜索路径、损坏文件和许可规则作出的安全设计建议，不是 Autodesk 声称的现成功能。

## 6. 可借鉴的开源项目与许可证

### 6.1 ezdxf：优先借鉴显式解析与替代策略

- 项目许可证为 MIT。[LICENSE，固定提交 `5265e6e`](https://github.com/mozman/ezdxf/blob/5265e6e6d02388a95f406c26983e5c39452ad003/LICENSE)
- `shapefile.py` 把 font 与 shape file 作为不同语义，解析 Unicode SHX 的 encoding/embedding 字段，并对无效结构抛出明确异常。[源码](https://github.com/mozman/ezdxf/blob/5265e6e6d02388a95f406c26983e5c39452ad003/src/ezdxf/fonts/shapefile.py)
- `fonts.py` 使用显式名称映射和可配置的本地解析顺序，只在本地字体管理器命中时选用替代，不包含“缺什么就联网下载什么”的路径。[源码](https://github.com/mozman/ezdxf/blob/5265e6e6d02388a95f406c26983e5c39452ad003/src/ezdxf/fonts/fonts.py)
- 项目示例明确把 SHX/SHP 支持标为尚未达到生产就绪，并因 Autodesk SHX 版权不能提交相关测试文件。因此适合借鉴分类、异常和显式映射思路，不应把它当成 AutoCAD 兼容性的证明。[示例源码](https://github.com/mozman/ezdxf/blob/5265e6e6d02388a95f406c26983e5c39452ad003/examples/render/shapefile_rendering.py)

### 6.2 mlightcad/shx-parser：借鉴类型白名单与边界检查

- 项目许可证为 MIT；README 声明支持 Shapes、Bigfont/Extended Big Font、Unifont，并采用按需解析和按字符缓存。[README，固定提交 `5100b5f`](https://github.com/mlightcad/shx-parser/blob/5100b5f29083d1f39d24ac57d68ec05cc702d647/README.md)、[LICENSE](https://github.com/mlightcad/shx-parser/blob/5100b5f29083d1f39d24ac57d68ec05cc702d647/LICENSE)
- 头解析器只接受已知字体类型；文件读取器在每次读取/跳转前检查缓冲区边界；内容解析器对无效记录数和不支持类型失败退出。[headerParser.ts](https://github.com/mlightcad/shx-parser/blob/5100b5f29083d1f39d24ac57d68ec05cc702d647/src/headerParser.ts)、[fileReader.ts](https://github.com/mlightcad/shx-parser/blob/5100b5f29083d1f39d24ac57d68ec05cc702d647/src/fileReader.ts)、[contentParser.ts](https://github.com/mlightcad/shx-parser/blob/5100b5f29083d1f39d24ac57d68ec05cc702d647/src/contentParser.ts)
- README 说明其从 `YFonts.SHX` 移植；若要复制实现或引入依赖，应继续核验上游来源与许可链。对 CADDC 更稳妥的做法是只借鉴“已知类型白名单、所有读取做边界检查、资源限额、缓存按内容身份隔离”的设计并独立实现。[上游来源链接](https://github.com/yzylovepmn/YFonts.SHX)

### 6.3 GNU LibreDWG：借鉴 DWG 依赖枚举，不直接并入闭源插件

- `dwggrep` 的 `match_STYLE()` 分别读取 STYLE 的主字体和 Big Font 字段，印证依赖审计必须把两者分列收集。[dwggrep.c，固定提交 `7fdd73f`](https://github.com/LibreDWG/libredwg/blob/7fdd73f39d74a78ac19e5629756a3c105349d8fd/programs/dwggrep.c)
- LibreDWG 使用 GPL-3.0-or-later。若 CADDC 不接受 GPL 分发义务，不应复制其实现、静态链接或把它当作普通可嵌入库；而且 CADDC 运行在 AutoCAD 内，已有官方数据库 API，无需为此引入 LibreDWG。[COPYING](https://github.com/LibreDWG/libredwg/blob/7fdd73f39d74a78ac19e5629756a3c105349d8fd/COPYING)

### 6.4 netDxf：.NET 格式边界的交叉验证

- `TextStyle` 只允许主字体为 TTF/SHX；Big Font 只能在主字体是 SHX 时设置，且 Big Font 本身也必须是 SHX。这适合作为“主字体与 Big Font 分开建模”的交叉验证。[TextStyle.cs，固定提交 `3d24d7c`](https://github.com/haplokuon/netDxf/blob/3d24d7c23418bd3a077d0e32a639f2ffe95bff51/netDxf/Tables/TextStyle.cs)
- 项目许可证为 MIT，但仓库已归档；适合参考或做离线 DXF 测试，不宜成为 CADDC 新功能的唯一长期依赖。[LICENSE](https://github.com/haplokuon/netDxf/blob/3d24d7c23418bd3a077d0e32a639f2ffe95bff51/LICENSE)、[仓库状态](https://github.com/haplokuon/netDxf)

## 7. 对 CADDC 的推荐功能边界

### 第一阶段：只做“缺失 SHX 审计助手”

建议输出一份结构化结果，每条至少包含：

- 请求文件名；
- 角色：普通文字、Big Font、shape/复杂线型；
- 引用来源：文字样式、线型、Shape 实体或 MTEXT 片段；
- 引用对象数量或样式/线型名称；
- AutoCAD 宿主是否找到、最终解析路径；
- 是否发生 `FONTMAP`/`FONTALT`/用户替代；
- 替代前后名称及风险提示。

操作按钮只建议提供：

1. 定位本地原文件；
2. 重新按 AutoCAD 搜索规则检查；
3. 显示 eTransmit 操作说明；
4. 为文字字体选择显式替代；
5. 定位 shape/复杂线型引用；
6. 忽略并只在当前会话继续。

第一阶段不要做：公网字体搜索、自动下载、静默改 `FONTALT`、静默改 `FONTMAP`、自动添加通用搜索路径、覆盖同名 SHX、自动保存被替代后的原 DWG。

### 第二阶段：可选的受控企业字体库

只有在公司确实拥有字体分发权并能维护清单时，再考虑受控字体库。它应以“批准清单 + 内容哈希 + 许可证元数据”为主键，不应以用户输入的文件名直接访问公网。安装和替代必须是显式、可审计、可回滚的操作。

## 8. 后续实现的验证标准

若据此开发功能，至少应分开验证：

1. 单元测试：普通 SHX、Big Font、shape SHX、复杂线型、同名多路径、缺失、损坏、超限和显式替代；
2. 宿主集成测试：`HostApplicationServices.FindFile()` 与实际 AutoCAD 搜索结果一致；
3. 安全测试：路径操纵、超大文件、截断记录、递归/循环形引用、解析超时和缓存碰撞；
4. 许可证测试：embedding 标志、无许可元数据、禁止分发和只读嵌入；
5. 真实 AutoCAD 人工冒烟：重新打开无错误提示，文字/符号/线型外观、对齐、打印结果与发送方基准一致。

静态测试或解析成功不能替代第 5 项；本次研究也未执行真实 AutoCAD UI 验证。

## 最终建议

CADDC 应把问题定义为“DWG 外部 SHX 依赖审计与显式恢复”，而不是“缺字体自动下载”。优先级应是：

1. 按普通字体、Big Font、shape/复杂线型分类；
2. 按 AutoCAD 宿主规则查找本地原文件；
3. 向原发送方索取已检查清单的 eTransmit 包；
4. 找不到原件时，仅对文字采用可见、可审计的映射或备用字体；
5. 对 shape/复杂线型定位并处理真实引用；
6. 默认不访问公网，未来如有企业字体库也必须受控、验签/验哈希、隔离解析并尊重字体许可证。
