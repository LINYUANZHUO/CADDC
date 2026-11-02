using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using YiZhiMao.Dc.Core;

if (args is ["--benchmark-line-rectangles"])
{
    BenchmarkLineRectangleDetector();
    return 0;
}

var tests = new (string Name, Action Test)[]
{
    ("重复快捷键不区分大小写", DuplicateAliasesAreRejected),
    ("非法快捷键被拒绝", InvalidAliasIsRejected),
    ("禁用默认快捷键后只生成自定义项", DisabledDefaultIsExcluded),
    ("同一命令默认和自定义快捷键相同时只注册一次", SameAliasOnSameCommandIsRegisteredOnce),
    ("LSP使用文档内命令并清理旧别名", LispContainsDocumentLocalCommandsAndCleanup),
    ("JSON合并保留用户配置并加入新命令", ConfigurationMergePreservesUserValues),
    ("旧版空自定义快捷键迁移为新默认值", LegacyEmptyCustomAliasMigratesToNewDefault),
    ("旧版未修改快捷键迁移到新的无冲突默认值", UnchangedLegacyAliasMigratesToNewDefault),
    ("旧版HJL迁移为不冲突的DCHJL", LegacyHingeAliasMigratesToDchjl),
    ("旧版墙板快捷键迁移为DC前缀", LegacyWainscotAliasesMigrateToDcPrefix),
    ("旧版柜体快捷键迁移为DC前缀", LegacyCabinetAliasesMigrateToDcPrefix),
    ("插件命令和默认快捷键使用DC前缀且保留已确认FQ", BuiltInCommandsAndAliasesUseDcPrefix),
    ("旧默认快捷键升级为DC前缀并保留用户修改", LegacyCommandAliasesMigrateWithoutOverwritingUserValues),
    ("非法用户配置仍可加载到面板修正", InvalidSavedAliasCanStillBeLoaded),
    ("空命令列表配置可恢复默认值", NullSavedItemsRecoverDefaults),
    ("新增映射使用DCNEWCMD默认值", NewMappingUsesRequestedDefaults),
    ("用户附加映射可随配置合并保留", UserDefinedMappingSurvivesConfigurationMerge),
    ("用户附加映射必须填写快捷键", UserDefinedMappingRequiresAlias),
    ("CSV导出使用UTF8并正确转义", CsvExportUsesUtf8AndEscapesFields),
    ("CAD命令大全包含预期分类和命令", CadCommandCatalogHasExpectedContent),
    ("CAD命令大全支持中英文搜索", CadCommandCatalogSearchesChineseAndEnglish),
    ("CAD命令大全空搜索显示全部", EmptyCadCommandSearchMatchesAll),
    ("批量图片参数支持默认值和等比缩放", ImageBatchOptionsScaleProportionally),
    ("批量图片参数拒绝非法间距和高度", ImageBatchOptionsRejectInvalidValues),
    ("批量图片按实际宽度和间距向右排列", ImageBatchOptionsCalculateNextPosition),
    ("标准单元柜参数保留三视图与工艺默认值", CabinetOptionsUseConfirmedDefaults),
    ("标准单元柜俯视图在正视图下方且侧视图在右侧", CabinetViewsUseConfirmedRelativePositions),
    ("独立柜体俯视图保留确认的默认参数", PlanCabinetOptionsUseConfirmedDefaults),
    ("独立柜体俯视图按门数联动门板与中竖板", PlanCabinetLayoutTracksDoorCount),
    ("独立柜体俯视图界面和DCFST/DCMBT2命令入口完整", PlanCabinetSurfaceCommandsAreComplete),
    ("柜体面板命令刷新与拆单数据入口完整", CabinetSurfaceCommandsAndStatisticsAreComplete),
    ("工具箱参数跨模块持久化并可完整恢复", ToolboxSettingsPersistAcrossModules),
    ("工具箱参数损坏或非法时安全回退默认值", ToolboxSettingsFallbackOnInvalidFiles),
    ("面板与底层命令共享同一持久化运行时", ToolboxRuntimeUsesSharedPersistentSettings),
    ("偏好设置默认全开并禁止关闭最后一个模块", ToolboxPreferencesPersistAndKeepOneModule),
    ("偏好设置导航启动和命令禁用入口完整", ToolboxPreferencesSurfaceAndGuardAreComplete),
    ("SHX同步只复制本图引用字体并安全处理重复与冲突", ShxFontSyncCopiesReferencedFontsSafely),
    ("缺失SHX按文字类型安全选择替换且不替换形文件", MissingShxReplacementPolicyIsSafe),
    ("格栅板参数保留默认值并拒绝非法尺寸", GrilleOptionsValidateDefaultsAndDimensions),
    ("格栅板竖向与横向布局按条宽和间距排列", GrilleLayoutSupportsVerticalAndHorizontalDirections),
    ("格栅板平面图与侧视图使用统一构造尺寸", GrilleLayoutBuildsRequestedAuxiliaryViews),
    ("洞洞板参数保留默认值并拒绝非法孔阵列", PerforatedPanelOptionsValidateDefaultsAndArray),
    ("洞洞板孔阵列按内偏移均匀分布", PerforatedPanelLayoutUsesInnerOffsets),
    ("洞洞板按需生成俯视图与侧视图", PerforatedPanelLayoutBuildsRequestedAuxiliaryViews),
    ("洞洞板只生成孔径距边和首个孔距代表标尺", PerforatedPanelLayoutBuildsRepresentativeDimensions),
    ("洞洞板直径标尺镜像到右上45度且箭头缩小六分之一", PerforatedDimensionsUseRequestedAngleAndArrowScale),
    ("洞洞板标尺文字缩半并显示起点偏移", PerforatedDimensionsUseRequestedTextAndExtensionScale),
    ("墙板格栅界面与DCGS/DCGST底层命令入口完整", WainscotGrilleSurfaceContractIsComplete),
    ("墙板洞洞板界面与DCDB/DCDBT底层命令入口完整", WainscotPerforatedSurfaceContractIsComplete),
    ("背景墙参数保留默认值并拒绝无效分排", BackgroundWallOptionsValidateDefaultsAndRows),
    ("背景墙按从下到上分排并支持等分宽度", BackgroundWallLayoutStacksRowsFromBottom),
    ("背景墙可选保留垂直排缝并生成全部排俯视图", BackgroundWallLayoutSupportsVerticalGapsAndAllPlanRows),
    ("背景墙随机宽度保持最小板宽并生成卡槽与辅助视图", BackgroundWallLayoutBuildsStableRandomSlotsAndViews),
    ("背景墙界面与DCBJQ底层命令入口完整", WainscotBackgroundWallSurfaceContractIsComplete),
    ("智能标注模块复用既有命令并安全同步标注样式", SmartAnnotationSurfaceAndSafeStyleSyncContractIsComplete),
    ("立面门板参数以柜体内空和四向盖板延伸值为基础", DoorPanelOptionsUseInnerOpeningAndCoverExtensions),
    ("门板布局把四向盖板延伸值应用到最终边界", DoorPanelLayoutAppliesCoverExtensions),
    ("立面与俯视门板统一使用门扇数补偿实际门缝", DoorPanelLayoutsUseUnifiedGapCompensation),
    ("门板内空识别从非闭合线段中选择点击柜格最近边界", DoorOpeningDetectorFindsNearestIndependentLineBay),
    ("门板内空识别拒绝不完整边界和无效尺寸", DoorOpeningDetectorRejectsIncompleteOrInvalidBounds),
    ("单门预览可以切换左右铰链方向", DoorPanelSingleDoorHingeSideCanBeSwitched),
    ("门板数量快捷键使用 CAD 原生消息入口唤醒预览", DoorPanelPreviewUsesCadNativeWakeUp),
    ("门板实时预览只绘制轻量线框并在确认后生成完整构件", DoorPanelPreviewDefersOpeningLinesAndHingesUntilConfirmation),
    ("门板默认入口必须完成两次对角点选择", DoorPanelDefaultFlowRequiresTwoCorners),
    ("门板抽面导航和立面门板参数入口完整", DoorPanelSurfaceContractIsComplete),
    ("门板数和门缝使用固定默认值", DoorPanelSurfaceUsesFixedDefaultCountAndGap),
    ("门板参数面板使用紧凑高密度布局", DoorPanelSurfaceUsesCompactLayout),
    ("俯视门板参数独立保存并校验厚度门缝前沿间距和盖值", PlanDoorPanelOptionsAreIndependentAndValidated),
    ("俯视门板固定任意门缝并按前沿间距生成", PlanDoorPanelLayoutUsesCursorSideAndFlip),
    ("俯视门板界面和DCMBF命令入口完整", PlanDoorPanelSurfaceAndCommandAreComplete),
    ("抽面默认两行一列且开启26毫米免拉手缝和遮罩", DrawerFaceOptionsUseConfirmedDefaults),
    ("抽面按二维行列均分并应用四向盖值", DrawerFaceLayoutSupportsVerticalAndHorizontalDirections),
    ("免拉手侧视图从缝隙边线或端点识别并生成C型和L型", HandlelessProfileDetectsRealBoundariesAndBuildsProfiles),
    ("抽面设置、智能识别和DCMC命令入口完整", DrawerFaceSurfaceAndCommandAreComplete),
    ("抽面首次预览刷新且Release构建包含Core", DrawerFacePreviewAndReleaseConfigurationAreComplete),
    ("抽面开门箭头向下且行列热键各自调整", DrawerFaceOpeningArrowAndHotkeysFollowDrawingWorkflow),
    ("门板线色遮罩与图层图纸模块入口完整", DoorColorMaskAndDrawingLayerSurfaceAreComplete),
    ("图纸修复遍历本地图块并重算墙板填充", DrawingDisplayRepairTraversesAllLocalBlocksAndReevaluatesHatches),
    ("图层图纸参数保留分类映射和智能选择图层", DrawingLayerOptionsPersistMappingsAndSelectionLayers),
    ("图层图纸面板包含自定义配置实体控制和智能选择", DrawingLayerSurfaceContainsConfigurationAndSelectionTools),
    ("图层图纸实体控制命令全部使用DC前缀", DrawingLayerEntityControlCommandsUseDcPrefixes),
    ("图层图纸智能选择按配置图层和对象特征筛选", DrawingLayerSmartSelectionUsesConfiguredLayersAndObjectTraits),
    ("板件模块包含板件单双线分区和组件四个页签", BoardModuleSurfaceAndCommandsAreComplete),
    ("板件标识默认比例为1并支持下置文字和玻璃三角标记", BoardTagUsesNormalizedScaleAndLowerText),
    ("收口与见光板按边缘和鼠标方向生成", BoardPlacementUsesEdgeAnchorsAndCursorDirection),
    ("收口截面支持L、T、I三种结构和四向旋转", BoardTrimSectionsSupportProfilesAndRotation),
    ("收口起点识别左右单线和双线且不依赖墙距", TrimStartDetectorRecognizesLocalLines),
    ("空间中分板按内空中心和鼠标方向自动对齐", CenterBoardAlignsToDetectedOpening),
    ("空间中分识别双线板件时采用靠近内空的边线", CenterBoardOpeningUsesInnerEdgesOfDoubleLines),
    ("位移板按鼠标四方向保持板边净距", OffsetBoardUsesClearSpacingOnFourSides),
    ("ABA夹心层板三层独立并以后端对齐形成内退", AbaBoardLayersKeepRearEdgesAligned),
    ("背板按鼠标后沿分别计算俯视缩进和正视延伸", BackboardUsesRearDirectionAndIndependentExtensions),
    ("板件高级操作包含位移夹层均分背板和编辑投影", AdvancedBoardOperationsAreComplete),
    ("板件单双线替换偏移数量精确", BoardLineReplacementOffsetsAreExact),
    ("单双三线按输入距离预览放线且延伸方向不受画线顺序影响", BoardLineOffsetsAndExtensionsFollowGeometry),
    ("门板分区辅助线按最大门宽和热键数量等分", DoorPartitionGuidesUseSmartAndAdjustedCounts),
    ("鞋柜斜层板按柜深智能增角并镜像生成前挡条", ShoeShelfLayoutFitsThinCabinetAndMirrorsFront),
    ("均分板件支持最大净空、临时格数、余量侧和严格等分", BoardDivisionSupportsSmartAndCountModes),
    ("分割板件按横竖板长向的两个位置生成三块", BoardSplitUsesTwoPositionsAlongLongAxis),
    ("板件分割和延剪拒绝无效几何", BoardEditingGuardsRejectInvalidGeometry),
    ("板件合并只允许完整相邻且兼容属性", BoardMergePropertiesMustMatch),
    ("板件延伸修剪按点取侧生成正确边界", BoardEditingUsesPickedSide),
    ("板件一键修剪自动选择点击端内侧最近交线", BoardNearestTrimFindsClickedSideBoundary),
    ("板件延伸修剪角点按边界方向选边且剪贴板占用可重试", BoardEditingHandlesCornerPickAndClipboardContention),
    ("板件单击边后就近延伸到外侧有效直线", BoardExtensionFindsNearestValidBoundary),
    ("层立板投影按参考板方向和深度范围生成", BoardProjectionUsesReferenceOrientationAndDepthRange),
    ("拆单统计菜单包含五类统计入口", StatisticsModuleSurfaceAndCommandsAreComplete),
    ("拆单统计高级参数与雷神BOM桥接完整", AdvancedStatisticsConfigurationAndBridgeAreComplete),
    ("拆单统计按规格汇总并输出Excel兼容CSV", DrawingStatisticsAggregateAndExport),
    ("一键全屋拆单支持CAD表格与Excel总表", WholeHouseStatisticsSupportsCadTableAndExcel),
    ("图框对齐让框内图形跟随所属图框移动", FrameAlignmentMovesContainedGeometry),
    ("圆盘默认三圈配置与命中规则稳定", RadialMenuDefaultsAndHitTestingAreStable),
    ("圆盘配置持久化并拒绝非法参数", RadialMenuOptionsPersistAndRejectInvalidValues),
    ("圆盘设置面板与底层命令入口完整", RadialMenuSurfaceAndCommandAreComplete),
    ("圆盘由鼠标中键双击触发并替代默认缩放范围", RadialMenuMiddleButtonActivationIsRegistered),
    ("LISP市场默认本地库与三种加载指南完整", LispMarketSurfaceContractIsComplete),
    ("本地LISP库扫描受支持文件并生成安全加载表达式", LispPluginLibraryScansAndBuildsLoadExpression),
    ("本地LISP命令在插件库中保持唯一", LispPluginCommandsAreUnique),
    ("一键图框参数校验并按内框计算缩放落位", FrameToolOptionsAndPlacementAreValid),
    ("一键加框合并与普通图形重叠的图块范围", OneClickFrameMergesOverlappingBlockBounds),
    ("图框模板目录只扫描DWG并保留外部选择", FrameTemplateLibraryScansDwgFiles),
    ("一键图框面板与四类底层命令入口完整", OneClickFrameSurfaceAndCommandsAreComplete),
    ("五金铰链页与门板页共享同一组参数", HardwareHingeTabSharesDoorPanelParameters),
    ("五金素材库创建目录并匹配同名预览", HardwareLibraryCreatesFoldersAndPairsPreviews),
    ("五金参数保留默认值并计算铰链数量", HardwareOptionsValidateAndCalculateHingeCount),
    ("铰链默认使用上下两端基准标注", HingeDefaultsToSplitFromEnds),
    ("铰链尺寸默认值与面板输入一致", HingeDimensionDefaultsMatchPanelInputs),
    ("挂衣杆按柜内净宽减尺并应用离顶高度", HangingRodLayoutUsesClearWidthAndTopOffset),
    ("挂衣杆从覆盖横线中选择最近上方线", HangingRodFindsNearestCoveringTopLine),
    ("挂衣杆按点击位置左右最近竖线限定当前柜格", HangingRodUsesClickedBayBoundaries),
    ("挂衣杆按动态块提交后的最终外包框校正", HangingRodFitsCommittedDynamicExtents),
    ("高门板第二铰链使用固定间距且其余均布", HingeLayoutUsesFixedSecondSpacing),
    ("左右铰链图形中心都位于门板内部", HingeCentersStayInsideDoor),
    ("盖板延伸后铰链中心越过层板端点仍会避让", HingeAvoidanceUsesPanelShelfOverlap),
    ("门板铰链只按柜体内空范围筛选避让层板", HingeAvoidanceUsesInnerOpeningScope),
    ("门板铰链先按柜内定位再补偿上下延伸", DoorPanelHingesCompensateVerticalExtensions),
    ("宽而薄的实体包围盒会识别为层板中心线", ThinEntityBoundsAreRecognizedAsShelf),
    ("门板命令通过实体包围盒补充识别层板", DoorPanelShelfCollectionUsesEntityBounds),
    ("铰链连续标注覆盖顶部间距中间间距和底部间距", HingeContinuousDimensionsCoverAllIntervals),
    ("铰链上下定位按就近端点分组且中点归顶部", HingeSplitDimensionsUseNearestPanelEnd),
    ("铰链标注不继承当前标注样式的全局比例", HingeDimensionsUseUnitScale),
    ("开门虚线从自由边向铰链侧汇合", DoorOpeningLinesConvergeOnHingeSide),
    ("铰链标尺从铰链边向门板内部展开", HingeDimensionsStayInsideDoorLeaf),
    ("铰链命令选择上下边线而非闭合矩形", HingeCommandSelectsDoorEdges),
    ("单独插入铰链复用尺寸参数并把标尺画在门内", StandaloneHingeDimensionsUsePanelOptions),
    ("标记铰链种类支持框选多个铰链圆", HingeLabelsSupportMultipleCircleSelection),
    ("DCHJL快捷键退出LISP后再启动底层命令", HingeShortcutQueuesCoreCommand),
    ("门板和铰链常用入口由原生命令兜底", NativeAliasesBypassLispLoadOrder),
    ("铰链类型文字默认35并按间距放入门内", HingeTypeLabelsUseConfigurableInsidePlacement),
    ("五金件面板和底层命令入口完整", HardwareSurfaceContractIsComplete),
    ("超级图库按一级目录分类并匹配同名预览", SuperLibraryCatalogScansCategoriesAndPreviews),
    ("超级图库分类与删除操作限制在图库根目录", SuperLibraryCatalogProtectsRootBoundary),
    ("超级图库位于批量打印下方并提供预览插入入口", SuperLibrarySurfaceAndCommandsAreComplete),
    ("Modules资源会打包并由插件直接读取", ModuleResourcesAreBundledAndReadDirectly),
    ("Bundle构建前清理全部旧Contents", BundleBuildCleansAllPreviousContents),
    ("AutoCAD 2017至2027安装清单按精确版本分流", AutoCadVersionManifestUsesExactLanes),
    ("工具箱XAML静态资源全部可解析", ShortcutPanelStaticResourcesAreDefined),
    ("快捷键面板保留双层导航和防截断约束", ShortcutPanelLayoutKeepsNavigationAndOverflowContract),
    ("打印设置可持久化并拒绝非法分辨率", PrintSettingsPersistAndValidate),
    ("打印选项只返回设备实际支持的值", PrintOptionsChooseOnlyAvailableValues),
    ("打印图框去重去嵌套并按阅读顺序排列", PrintFramesAreCleanedAndSorted),
    ("批量打印逐份与非逐份顺序正确", PrintCopyOrderMatchesCollateSetting),
    ("PDF工具扫描页数并按选择顺序合并", PdfToolScansAndMergesInOrder),
    ("批量打印命令面板与PDF工具入口完整", BatchPrintSurfaceContractIsComplete),
    ("批量打印避免布局字典强转并保留阶段错误", BatchPrintRuntimeRegressionContract),
    ("批量打印窄面板按钮与窗口坐标保持有效", BatchPrintNarrowPanelAndWindowAreaContract),
    ("安装器检测到AutoCAD后可重试、安全强制关闭或取消", InstallerAutoCadShutdownFlowIsSafe),
    ("安装向导支持标准安装范围、进度和完成路径", InstallerWizardSupportsScopePathAndProgress),
    ("安装升级只恢复用户新增或修改的资源", InstallerUpgradePreservesUserResources),
    ("卸载默认保留用户资源并可显式清除用户数据", InstallerUninstallPreservesDataByDefault),
    ("安装失败会回滚旧Bundle并记录日志", InstallerFailureRollsBackAndWritesLog),
    ("批量打印按布局读取纸张并先写窗口后切换类型", BatchPrintUsesLayoutBoundMediaAndWindowSequence),
    ("纸张打印排除文件设备且批量任务关闭后台打印", BatchPrintSeparatesPaperDevicesAndDisablesBackgroundPlot),
    ("D3和D4从模型空间自动切换首个布局且D1提示打印服务前置条件", BatchPrintTargetsCurrentLayoutAndChecksPaperPrerequisites),
    ("打印预览生成临时PDF并自动打开且不受纸张服务阻断", BatchPrintPreviewGeneratesAndOpensPdf),
    ("散线图框检测使用容差索引且不重复", LineRectangleDetectorUsesToleranceIndexWithoutDuplicates),
    ("安全依赖与面板计时器生命周期已加固", SecurityDependencyAndPanelTimerAreHardened)
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"[PASS] {name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"[FAIL] {name}: {exception.Message}");
    }
}

Console.WriteLine($"测试结果：{tests.Length - failed}/{tests.Length} 通过");
return failed == 0 ? 0 : 1;

static void DuplicateAliasesAreRejected()
{
    var items = new[]
    {
        CreateItem("命令A", "DC_CMD_A", "AA"),
        CreateItem("命令B", "DC_CMD_B", customAlias: "aa")
    };

    var errors = ShortcutValidator.Validate(items);
    Assert(errors.Any(error => error.Contains("重复", StringComparison.Ordinal)), "未检测到大小写不同的重复快捷键。");
}

static void InvalidAliasIsRejected()
{
    var errors = ShortcutValidator.Validate([
        CreateItem("命令A", "DC_CMD_A", customAlias: "A)(ALERT")
    ]);

    Assert(errors.Any(error => error.Contains("无效", StringComparison.Ordinal)), "未拒绝可破坏LSP语法的快捷键。");
}

static void DisabledDefaultIsExcluded()
{
    var item = CreateItem("命令A", "DC_CMD_A", "AA", "AB");
    item.DisableDefault = true;

    var aliases = ShortcutValidator.GetEffectiveAliases(item);
    Assert(aliases.SequenceEqual(["AB"]), "禁用默认快捷键后仍返回了默认项。");
}

static void SameAliasOnSameCommandIsRegisteredOnce()
{
    var item = CreateItem("命令A", "DC_CMD_A", "AA", "aa");

    var aliases = ShortcutValidator.GetEffectiveAliases(item);
    Assert(aliases.SequenceEqual(["AA"]), "同一命令的相同默认和自定义快捷键未去重。");
    Assert(ShortcutValidator.Validate([item]).Count == 0, "同一命令的相同快捷键被错误判定为跨命令重复。");
}

static void LispContainsDocumentLocalCommandsAndCleanup()
{
    var item = CreateItem("命令A", "DC_CMD_A", "AA", "AB");
    item.DisableDefault = true;
    var script = AutoLispShortcutGenerator.Generate(new ShortcutConfiguration { ShortcutItems = [item] });

    Assert(script.Contains("(set (read (strcat \"C:\" dc:alias)) nil)", StringComparison.Ordinal),
        "LSP缺少当前文档旧快捷键清理逻辑。");
    Assert(script.Contains("(defun c:AB", StringComparison.Ordinal),
        "LSP没有把快捷键定义为当前文档命令。");
    Assert(!script.Contains("vlax-add-cmd", StringComparison.Ordinal) &&
           !script.Contains("vlax-remove-cmd", StringComparison.Ordinal),
        "LSP仍使用会在多文档间产生所有权冲突的全局Visual LISP命令注册。");
    Assert(script.Contains("\"AB\"", StringComparison.Ordinal), "LSP缺少自定义快捷键。");
    Assert(!script.Contains("\"AA\"", StringComparison.Ordinal), "LSP仍包含已禁用的默认快捷键。");
    Assert(script.Contains(".DC_CMD_A", StringComparison.Ordinal), "LSP未调用底层命令。");
}

static void ConfigurationMergePreservesUserValues()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        var store = new ShortcutConfigurationStore(path);
        var first = store.LoadAndMerge([CreateItem("命令A", "DC_CMD_A", "AA")]);
        first.ShortcutItems[0].Description = "用户修改的说明";
        first.ShortcutItems[0].DefaultAlias = "AZ";
        first.ShortcutItems[0].CustomAlias = "AX";
        first.ShortcutItems[0].DisableDefault = true;
        store.Save(first);

        var merged = store.LoadAndMerge([
            CreateItem("命令A新名称", "DC_CMD_A", "AA"),
            CreateItem("命令B", "DC_CMD_B", "BB")
        ]);

        Assert(merged.ShortcutItems.Count == 2, "未自动加入新增底层命令。");
        Assert(merged.ShortcutItems[0].Description == "用户修改的说明", "用户修改的功能说明未保留。");
        Assert(merged.ShortcutItems[0].DefaultAlias == "AZ", "用户修改的默认快捷键未保留。");
        Assert(merged.ShortcutItems[0].CustomAlias == "AX", "用户自定义快捷键未保留。");
        Assert(merged.ShortcutItems[0].DisableDefault, "禁用默认快捷键设置未保留。");
    }
    finally
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}

static void LegacyEmptyCustomAliasMigratesToNewDefault()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        File.WriteAllText(path, """
            {
              "SchemaVersion": 1,
              "ShortcutItems": [
                {
                  "Category": "测试",
                  "Description": "命令A",
                  "CoreCommand": "DC_CMD_A",
                  "DefaultAlias": "",
                  "CustomAlias": "",
                  "DisableDefault": false
                }
              ]
            }
            """);

        var store = new ShortcutConfigurationStore(path);
        var configuration = store.LoadAndMerge([CreateItem("命令A", "DC_CMD_A", "JC", "JC")]);

        Assert(configuration.SchemaVersion == ShortcutConfiguration.CurrentSchemaVersion, "旧配置未升级到当前版本。");
        Assert(configuration.ShortcutItems[0].DefaultAlias == "JC", "旧配置未采用新的默认快捷键。");
        Assert(configuration.ShortcutItems[0].CustomAlias == "JC", "旧版空自定义快捷键未迁移为新的初始值。");
        Assert(File.ReadAllText(path).Contains(
                $"\"SchemaVersion\": {ShortcutConfiguration.CurrentSchemaVersion}",
                StringComparison.Ordinal),
            "迁移后的配置未写回磁盘。");
    }
    finally
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}

static void UnchangedLegacyAliasMigratesToNewDefault()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        File.WriteAllText(path, """
            {
              "SchemaVersion": 4,
              "ShortcutItems": [
                {
                  "Category": "五金件",
                  "Description": "插入挂衣杆",
                  "CoreCommand": "DC_CMD_HANGING_ROD",
                  "DefaultAlias": "YG",
                  "CustomAlias": "YG",
                  "DisableDefault": false
                }
              ]
            }
            """);

        var store = new ShortcutConfigurationStore(path);
        var configuration = store.LoadAndMerge([
            CreateItem("插入挂衣杆", "DC_CMD_HANGING_ROD", "DCYG", "DCYG")
        ]);
        var item = configuration.ShortcutItems.Single();
        var script = AutoLispShortcutGenerator.Generate(configuration);

        Assert(item.DefaultAlias == "DCYG", "升级后仍保留了与雷神冲突的旧默认快捷键。");
        Assert(item.CustomAlias == "DCYG", "未修改的旧自定义快捷键没有跟随新默认值迁移。");
        Assert(script.Contains("(defun c:DCYG", StringComparison.Ordinal), "迁移后未生成 DCYG 快捷键。");
        Assert(!script.Contains("(defun c:YG", StringComparison.Ordinal), "迁移后仍生成冲突的 YG 快捷键。");
    }
    finally
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}

static void LegacyHingeAliasMigratesToDchjl()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        File.WriteAllText(path, """
            {
              "SchemaVersion": 5,
              "ShortcutItems": [
                {
                  "Category": "门板抽面",
                  "Description": "标记铰链种类",
                  "CoreCommand": "DC_CMD_HINGE_LABEL",
                  "DefaultAlias": "HJL",
                  "CustomAlias": "HJL",
                  "DisableDefault": false
                }
              ]
            }
            """);

        var store = new ShortcutConfigurationStore(path);
        var configuration = store.LoadAndMerge([
            CreateItem("标记铰链种类", "DC_CMD_HINGE_LABEL", "DCHJL", "DCHJL")
        ]);
        var item = configuration.ShortcutItems.Single();
        var script = AutoLispShortcutGenerator.Generate(configuration);

        Assert(item.DefaultAlias == "DCHJL" && item.CustomAlias == "DCHJL",
            "未修改的旧 HJL 配置没有迁移到 DCHJL。" );
        Assert(script.Contains("(defun c:DCHJL", StringComparison.Ordinal) &&
               !script.Contains("(defun c:HJL", StringComparison.Ordinal),
            "迁移后仍生成会被雷神覆盖的 HJL 定义。" );
    }
    finally
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}

static void LegacyWainscotAliasesMigrateToDcPrefix()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        File.WriteAllText(path, """
            {
              "SchemaVersion": 6,
              "ShortcutItems": [
                {
                  "Category": "墙板",
                  "Description": "生成格栅板",
                  "CoreCommand": "DC_CMD_WAINSCOT_GRILLE_DRAW",
                  "DefaultAlias": "GS",
                  "CustomAlias": "GS",
                  "DisableDefault": false
                },
                {
                  "Category": "墙板",
                  "Description": "内空填充格栅",
                  "CoreCommand": "DC_CMD_WAINSCOT_GRILLE_FILL",
                  "DefaultAlias": "GST",
                  "CustomAlias": "GST",
                  "DisableDefault": false
                }
              ]
            }
            """);

        var store = new ShortcutConfigurationStore(path);
        var configuration = store.LoadAndMerge([
            CreateItem("生成格栅板", "DC_CMD_WAINSCOT_GRILLE_DRAW", "DCGS", "DCGS"),
            CreateItem("内空填充格栅", "DC_CMD_WAINSCOT_GRILLE_FILL", "DCGST", "DCGST")
        ]);
        var script = AutoLispShortcutGenerator.Generate(configuration);

        Assert(configuration.ShortcutItems.Select(item => item.DefaultAlias).SequenceEqual(["DCGS", "DCGST"]),
            "墙板默认快捷键未迁移为 DC 前缀。" );
        Assert(configuration.ShortcutItems.Select(item => item.CustomAlias).SequenceEqual(["DCGS", "DCGST"]),
            "未修改的墙板自定义快捷键未跟随新默认值迁移。" );
        Assert(script.Contains("(defun c:DCGS", StringComparison.Ordinal) &&
               script.Contains("(defun c:DCGST", StringComparison.Ordinal) &&
               !script.Contains("(defun c:GS ", StringComparison.Ordinal) &&
               !script.Contains("(defun c:GST ", StringComparison.Ordinal),
            "迁移后的 LSP 仍包含无 DC 前缀的墙板快捷键。" );
    }
    finally
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}

static void LegacyCabinetAliasesMigrateToDcPrefix()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        File.WriteAllText(path, """
            {
              "SchemaVersion": 9,
              "ShortcutItems": [
                {
                  "Category": "柜体",
                  "Description": "一键生成单元柜",
                  "CoreCommand": "DC_CMD_CABINET_STANDARD",
                  "DefaultAlias": "DG",
                  "CustomAlias": "DG",
                  "DisableDefault": false
                },
                {
                  "Category": "柜体",
                  "Description": "全局刷新柜体/板件",
                  "CoreCommand": "DC_CMD_CABINET_REFRESH",
                  "DefaultAlias": "XX",
                  "CustomAlias": "XX",
                  "DisableDefault": false
                },
                {
                  "Category": "柜体",
                  "Description": "生成万能柜体",
                  "CoreCommand": "DC_CMD_CABINET_UNIVERSAL",
                  "DefaultAlias": "WNG",
                  "CustomAlias": "WNG",
                  "DisableDefault": false
                }
              ]
            }
            """);

        var configuration = new ShortcutConfigurationStore(path).LoadAndMerge([
            CreateItem("一键生成单元柜", "DC_CMD_CABINET_STANDARD", "DCDG", "DCDG"),
            CreateItem("全局刷新柜体/板件", "DC_CMD_CABINET_REFRESH", "DCXX", "DCXX"),
            CreateItem("生成万能柜体", "DC_CMD_CABINET_UNIVERSAL", "DCWNG", "DCWNG")
        ]);

        Assert(configuration.ShortcutItems.Select(item => item.DefaultAlias)
                   .SequenceEqual(["DCDG", "DCXX", "DCWNG"]) &&
               configuration.ShortcutItems.Select(item => item.CustomAlias)
                   .SequenceEqual(["DCDG", "DCXX", "DCWNG"]),
            "旧版柜体快捷键没有随默认值迁移到 DC 前缀。" );
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void BuiltInCommandsAndAliasesUseDcPrefix()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var pluginRoot = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin");
    var source = string.Join(
        Environment.NewLine,
        Directory.EnumerateFiles(pluginRoot, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

    var shortcutAliases = Regex.Matches(
            source,
            @"\[ShortcutCommand\([^\r\n]*?,\s*""(?<alias>[A-Z0-9_]+)""\)\]")
        .Select(match => match.Groups["alias"].Value)
        .ToArray();
    var commandNames = Regex.Matches(
            source,
            @"\[CommandMethod\(""(?<command>[A-Z0-9_]+)""")
        .Select(match => match.Groups["command"].Value)
        .ToArray();

    Assert(shortcutAliases.Length > 0 && shortcutAliases.All(alias =>
            alias.StartsWith("DC", StringComparison.Ordinal) || alias == "FQ"),
        "存在未使用 DC 前缀且未经确认的插件默认快捷键。" );
    Assert(commandNames.Length > 0 && commandNames.All(command => command.StartsWith("DC", StringComparison.Ordinal)),
        "存在未使用 DC 前缀的插件公开命令。" );
}

static void LegacyCommandAliasesMigrateWithoutOverwritingUserValues()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        File.WriteAllText(path, """
            {
              "SchemaVersion": 7,
              "ShortcutItems": [
                {
                  "CoreCommand": "DC_CMD_STATUS",
                  "DefaultAlias": "JC",
                  "CustomAlias": "JC"
                },
                {
                  "CoreCommand": "DC_CMD_IMAGE_BATCH",
                  "DefaultAlias": "OLE",
                  "CustomAlias": "MYIMAGE"
                },
                {
                  "CoreCommand": "DC_CMD_LAYER_MANAGER",
                  "DefaultAlias": "MYLAYER",
                  "CustomAlias": "MYLAYER2"
                },
                {
                  "CoreCommand": "DC_CMD_LINE_TO_DOUBLE",
                  "DefaultAlias": "DCZ2",
                  "CustomAlias": "DCZ2"
                },
                {
                  "CoreCommand": "DC_CMD_LINE_TO_TRIPLE",
                  "DefaultAlias": "DCZ3",
                  "CustomAlias": "MYTRIPLE"
                }
              ]
            }
            """);

        var store = new ShortcutConfigurationStore(path);
        var configuration = store.LoadAndMerge([
            CreateItem("检查插件运行状态", "DC_CMD_STATUS", "DCJC", "DCJC"),
            CreateItem("批量插入图片", "DC_CMD_IMAGE_BATCH", "DCOLE", "DCOLE"),
            CreateItem("快速图层管理器", "DC_CMD_LAYER_MANAGER", "DCTC", "DCTC"),
            CreateItem("单线变双", "DC_CMD_LINE_TO_DOUBLE", "DC22", "DC22"),
            CreateItem("单线变三", "DC_CMD_LINE_TO_TRIPLE", "DC33", "DC33")
        ]);

        var status = configuration.ShortcutItems.Single(item => item.CoreCommand == "DC_CMD_STATUS");
        var image = configuration.ShortcutItems.Single(item => item.CoreCommand == "DC_CMD_IMAGE_BATCH");
        var layer = configuration.ShortcutItems.Single(item => item.CoreCommand == "DC_CMD_LAYER_MANAGER");
        var lineToDouble = configuration.ShortcutItems.Single(item => item.CoreCommand == "DC_CMD_LINE_TO_DOUBLE");
        var lineToTriple = configuration.ShortcutItems.Single(item => item.CoreCommand == "DC_CMD_LINE_TO_TRIPLE");
        Assert(status.DefaultAlias == "DCJC" && status.CustomAlias == "DCJC",
            "未修改的 JC 默认值没有迁移为 DCJC。" );
        Assert(image.DefaultAlias == "DCOLE" && image.CustomAlias == "MYIMAGE",
            "OLE 默认值未迁移，或用户自定义快捷键被覆盖。" );
        Assert(layer.DefaultAlias == "MYLAYER" && layer.CustomAlias == "MYLAYER2",
            "用户编辑过的默认和自定义快捷键没有保留。" );
        Assert(lineToDouble.DefaultAlias == "DC22" && lineToDouble.CustomAlias == "DC22" &&
               lineToTriple.DefaultAlias == "DC33" && lineToTriple.CustomAlias == "MYTRIPLE",
            "单双线旧快捷命令没有迁移为 DC22/DC33，或用户自定义快捷键被覆盖。" );
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void InvalidSavedAliasCanStillBeLoaded()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        File.WriteAllText(path, """
            {
              "SchemaVersion": 1,
              "ShortcutItems": [
                {
                  "Category": "测试",
                  "Description": "命令A",
                  "CoreCommand": "DC_CMD_A",
                  "DefaultAlias": "AA",
                  "CustomAlias": "A)(ALERT",
                  "DisableDefault": false
                }
              ]
            }
            """);

        var store = new ShortcutConfigurationStore(path);
        var configuration = store.LoadAndMerge([
            CreateItem("命令A", "DC_CMD_A", "AA"),
            CreateItem("命令B", "DC_CMD_B", "BB")
        ]);

        Assert(configuration.ShortcutItems.Count == 2, "目录合并失败，面板无法展示最新命令。");
        Assert(configuration.ShortcutItems[0].CustomAlias == "A)(ALERT", "非法输入被静默丢弃，用户无法在面板中定位修正。");
        Assert(ShortcutValidator.Validate(configuration.ShortcutItems).Count > 0, "非法输入未保留为可见校验错误。");
    }
    finally
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}

static void NullSavedItemsRecoverDefaults()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        File.WriteAllText(path, "{\"SchemaVersion\":1,\"ShortcutItems\":null}");

        var store = new ShortcutConfigurationStore(path);
        var configuration = store.LoadAndMerge([CreateItem("命令A", "DC_CMD_A", "AA")]);

        Assert(configuration.ShortcutItems.Count == 1, "空命令列表未恢复代码中的默认命令。");
        Assert(File.ReadAllText(path).Contains("DC_CMD_A", StringComparison.Ordinal), "恢复后的默认命令未写回配置文件。");
    }
    finally
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}

static void NewMappingUsesRequestedDefaults()
{
    var item = ShortcutItem.CreateNewMapping();

    Assert(item.Description == "新功能", "新增映射的功能说明默认值不正确。");
    Assert(item.DefaultAlias == "DCNEWCMD", "新增映射的默认快捷键不正确。");
    Assert(item.CustomAlias == "DCNEWCMD", "新增映射的自定义快捷键不正确。");
    Assert(item.CoreCommand == "DCNEWCMD", "新增映射的底层命令不正确。");
    Assert(item.IsUserDefined, "新增映射未标记为用户映射。");
    Assert(ShortcutValidator.GetEffectiveAliases(item).SequenceEqual(["DCNEWCMD"]), "相同的默认和自定义快捷键未合并为一个映射。");

    var script = AutoLispShortcutGenerator.Generate(new ShortcutConfiguration { ShortcutItems = [item] });
    Assert(script.Contains(".DCNEWCMD", StringComparison.Ordinal), "新增映射生成的LSP未调用DCNEWCMD底层命令。");
}

static void UserDefinedMappingSurvivesConfigurationMerge()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "ShortcutConfig.json");
        var store = new ShortcutConfigurationStore(path);
        var configuration = store.LoadAndMerge([CreateItem("命令A", "DC_CMD_A", "AA")]);
        var newMapping = ShortcutItem.CreateNewMapping();
        newMapping.Description = "用户新命令";
        newMapping.DefaultAlias = "NEWONE";
        newMapping.CustomAlias = "NEWONE2";
        configuration.ShortcutItems.Add(newMapping);
        store.Save(configuration);

        var merged = store.LoadAndMerge([CreateItem("命令A新名称", "DC_CMD_A", "AA")]);
        var userMapping = merged.ShortcutItems.Single(item => item.IsUserDefined);

        Assert(merged.ShortcutItems.Count == 2, "用户附加映射未随配置合并保留。");
        Assert(userMapping.Description == "用户新命令", "用户映射的功能说明未保留。");
        Assert(userMapping.DefaultAlias == "NEWONE", "用户映射的默认快捷键未保留。");
        Assert(userMapping.CustomAlias == "NEWONE2", "用户映射的自定义快捷键未保留。");
        Assert(userMapping.CoreCommand == "DCNEWCMD", "用户映射的底层命令未保留。");
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void UserDefinedMappingRequiresAlias()
{
    var item = CreateItem("命令A（附加映射）", "NEWCMD");
    item.IsUserDefined = true;

    var errors = ShortcutValidator.Validate([item]);

    Assert(errors.Any(error => error.Contains("至少需要一个可用快捷键", StringComparison.Ordinal)),
        "空的用户映射未被拒绝。");
}

static void CsvExportUsesUtf8AndEscapesFields()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "快捷键.csv");
        ShortcutCsvExporter.Export(path,
        [
            CreateItem("命令A,\"测试\"", "DC_CMD_A", "AA", "AX"),
            ShortcutItem.CreateNewMapping()
        ]);

        var bytes = File.ReadAllBytes(path);
        Assert(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "CSV未写入UTF-8 BOM，Excel可能无法正确识别中文。");
        var text = Encoding.UTF8.GetString(bytes.AsSpan(Encoding.UTF8.Preamble.Length));
        Assert(text.StartsWith("功能说明,默认,自定义,禁用默认,底层命令\r\n", StringComparison.Ordinal), "CSV表头不正确。");
        Assert(text.Contains("\"命令A,\"\"测试\"\"\",AA,AX,否,DC_CMD_A\r\n", StringComparison.Ordinal), "CSV未正确转义逗号或双引号。");
        Assert(text.Contains("新功能,DCNEWCMD,DCNEWCMD,否,DCNEWCMD\r\n", StringComparison.Ordinal), "CSV缺少新增映射默认值。");
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void CadCommandCatalogHasExpectedContent()
{
    var commands = CadCommandCatalog.GetAll();

    Assert(commands.Count >= 90, "CAD命令目录数量不足。") ;
    Assert(commands.Any(item => item.Category == "修改命令" && item.FullCommand == "ARRAY"), "缺少修改命令分类。") ;
    Assert(commands.Any(item => item.Category == "尺寸标注" && item.FullCommand == "DIMALIGNED"), "缺少尺寸标注分类。") ;
    Assert(commands.Any(item => item.Category == "绘图命令" && item.FullCommand == "CIRCLE"), "缺少绘图命令分类。") ;
    Assert(commands.All(item => item.Category.Length > 0 &&
                                item.Shortcut.Length > 0 &&
                                item.FullCommand.Length > 0 &&
                                item.Description.Length > 0), "CAD命令目录存在空字段。") ;
}

static void CadCommandCatalogSearchesChineseAndEnglish()
{
    var commands = CadCommandCatalog.GetAll();
    var byEnglish = commands.Where(item => CadCommandCatalog.Matches(item, "dimaligned")).ToList();
    var byChinese = commands.Where(item => CadCommandCatalog.Matches(item, "圆弧")).ToList();

    Assert(byEnglish.Count == 1 && byEnglish[0].Shortcut == "DAL", "完整命令搜索结果不正确。") ;
    Assert(byChinese.Any(item => item.FullCommand == "ARC"), "中文功能说明搜索结果不正确。") ;
}

static void EmptyCadCommandSearchMatchesAll()
{
    var commands = CadCommandCatalog.GetAll();

    Assert(commands.All(item => CadCommandCatalog.Matches(item, "  ")), "空搜索未匹配全部CAD命令。") ;
    Assert(commands.All(item => !CadCommandCatalog.Matches(item, "不存在的命令关键字")), "不存在的关键字产生了错误匹配。") ;
}

static void ImageBatchOptionsScaleProportionally()
{
    Assert(ImageBatchOptions.TryCreate("50.0", true, "1000", out var options, out var error),
        $"默认图片参数未通过校验：{error}");

    var size = options.CalculateTargetSize(1600, 800);
    Assert(options == ImageBatchOptions.Default, "图片参数默认值与参考功能不一致。");
    Assert(size == new ImagePixelSize(2000, 1000), "统一高度未保持原始宽高比。");

    var originalSize = new ImageBatchOptions(20, false, 1000).CalculateTargetSize(640, 480);
    Assert(originalSize == new ImagePixelSize(640, 480), "关闭统一高度后仍改变了图片像素尺寸。");
}

static void ImageBatchOptionsRejectInvalidValues()
{
    Assert(!ImageBatchOptions.TryCreate("-1", true, "1000", out _, out var gapError) &&
           gapError.Contains("排列间距", StringComparison.Ordinal), "负数间距未被拒绝。");
    Assert(!ImageBatchOptions.TryCreate("50", true, "0", out _, out var heightError) &&
           heightError.Contains("统一像素高度", StringComparison.Ordinal), "零像素高度未被拒绝。");
    Assert(ImageBatchOptions.TryCreate("50", false, "无效值", out var disabledOptions, out _),
        "关闭统一高度后仍错误要求填写像素高度。");
    Assert(disabledOptions.PixelHeight == ImageBatchOptions.DefaultPixelHeight,
        "关闭统一高度时未保留安全的默认像素高度。");
}

static void ImageBatchOptionsCalculateNextPosition()
{
    var options = new ImageBatchOptions(50, true, 1000);

    Assert(Math.Abs(options.CalculateNextX(100, 240) - 390) < 0.000001,
        "下一张图片位置未按实际宽度加间距计算。");
    AssertThrows<ArgumentOutOfRangeException>(() => options.CalculateNextX(100, 0),
        "零宽度图片未被拒绝。");
}

static void CabinetOptionsUseConfirmedDefaults()
{
    var options = CabinetOptions.Default;
    var topRailAuxiliaryDepth = typeof(CabinetOptions)
        .GetProperty("TopRailAuxiliaryDepth")?
        .GetValue(options);

    Assert(options.GenerateElevation && options.GeneratePlan && options.GenerateSide &&
           options.PlanViewSpacing == 400 && options.SideViewSpacing == 400 &&
           options.Width == 900 && options.Height == 2100 && options.Depth == 580 &&
           options.Thickness == 18 && options.BackboardThickness == 18 &&
           options.BackboardTopExtension == 0 && options.BackboardBottomExtension == 0 &&
           options.TopRailHeight == 50 && topRailAuxiliaryDepth is 70d &&
           options.FrontToeKickHeight == 80 &&
           options.RearToeKickHeight == 80 && options.RearToeKickRearInset == 20 &&
           options.TryValidate(out _),
        "标准单元柜默认尺寸、三视图开关或工艺参数与确认界面不一致。" );

    Assert(!(options with { GenerateElevation = false, GeneratePlan = false, GenerateSide = false })
               .TryValidate(out _),
        "标准单元柜错误地允许关闭全部视图。" );
    Assert(!(options with { TopFrontInset = 400, TopRearInset = 300 }).TryValidate(out _),
        "标准单元柜错误地接受了超过柜深的顶板缩进。" );

    var root = Path.Combine(Path.GetTempPath(), $"dc-cabinet-default-migration-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        var path = Path.Combine(root, "ToolboxSettings.json");
        File.WriteAllText(path, """
            {
              "Version": 1,
              "Cabinet": {
                "Width": 2400,
                "Height": 1000,
                "Depth": 620,
                "Thickness": 18.5,
                "PlanViewSpacing": 350
              }
            }
            """);
        var migrated = new ToolboxSettingsStore(path).Load();
        Assert(migrated.Version == 5 && migrated.Cabinet.Width == 900 &&
               migrated.Cabinet.Height == 2100 && migrated.Cabinet.Depth == 580 &&
               migrated.Cabinet.Thickness == 18 &&
               migrated.Cabinet.PlanViewSpacing == 350,
            "旧版默认柜体尺寸没有迁移，或迁移时覆盖了用户调整的其他参数。" );
        File.WriteAllText(path, """
            {
              "Version": 2,
              "Cabinet": {
                "Width": 900,
                "Height": 2100,
                "Depth": 600,
                "Thickness": 18.5,
                "PlanViewSpacing": 350
              }
            }
            """);
        migrated = new ToolboxSettingsStore(path).Load();
        Assert(migrated.Version == 5 && migrated.Cabinet.Depth == 580 &&
               migrated.Cabinet.Thickness == 18 && migrated.Cabinet.PlanViewSpacing == 350,
            "Version 2 cabinet defaults were not migrated without disturbing other values.");
        File.WriteAllText(path, """
            {
              "Version": 3,
              "Cabinet": {
                "Width": 900,
                "Height": 2100,
                "Depth": 580,
                "Thickness": 18,
                "BackboardTopExtension": 18,
                "BackboardBottomExtension": 18,
                "PlanViewSpacing": 350
              }
            }
            """);
        migrated = new ToolboxSettingsStore(path).Load();
        Assert(migrated.Version == 5 && migrated.Cabinet.BackboardTopExtension == 0 &&
               migrated.Cabinet.BackboardBottomExtension == 0 &&
               migrated.Cabinet.PlanViewSpacing == 350,
            "Version 3 backboard extension defaults were not migrated without disturbing other values.");
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void CabinetViewsUseConfirmedRelativePositions()
{
    var options = CabinetOptions.Default;
    var layout = CabinetLayoutCalculator.Calculate(options);

    Assert(layout.Parts.Count == 9 && layout.Shapes.Count == 18 &&
           layout.PrimaryView == CabinetViewKind.Elevation,
        "标准单元柜没有保留八类板件，或三视图仍在重复投影不可见板件。" );
    Assert(Math.Abs(layout.ElevationBounds.Bottom - layout.PlanBounds.Top - 400) < 0.000001,
        "俯视图没有按净距 400 放在正视图下方。" );
    Assert(Math.Abs(layout.SideBounds.Left - layout.ElevationBounds.Right - 400) < 0.000001,
        "侧视图没有按净距 400 放在正视图右侧。" );
    Assert(Math.Abs(layout.PlanBounds.Left - layout.ElevationBounds.Left) < 0.000001 &&
           Math.Abs(layout.SideBounds.Bottom - layout.ElevationBounds.Bottom) < 0.000001,
        "俯视图未与正视图左对齐，或侧视图未与正视图下沿对齐。" );

    var bottom = layout.Parts.Single(part => part.Kind == CabinetPartKind.Bottom);
    Assert(bottom.Box.MinY == 80 && bottom.Box.MaxY == 98 &&
           bottom.CutWidth == 864 && bottom.CutHeight == 580 && bottom.CutThickness == 18,
        "底板没有定位在踢脚线之上，或拆单净尺寸计算错误。" );

    var backboard = layout.Parts.Single(part => part.Kind == CabinetPartKind.Backboard);
    Assert(backboard.Box.MinX == 18 && backboard.Box.MaxX == 882 &&
           backboard.Box.MinY == 98 && backboard.Box.MaxY == 2032 &&
           backboard.CutWidth == 864 && backboard.CutHeight == 1934,
        "Backboard must be based on the inner opening before its extensions are applied.");

    var topRail = layout.Parts.Single(part => part.Kind == CabinetPartKind.TopRail);
    Assert(topRail.Box.MinX == 0 && topRail.Box.MaxX == 900 &&
           topRail.Box.MinY == 2050 && topRail.Box.MaxY == 2100 &&
           topRail.Box.MinZ == 0 && topRail.Box.MaxZ == 18 &&
           topRail.CutWidth == 900 && topRail.CutHeight == 50 && topRail.CutThickness == 18,
        "Top rail must sit above the top board while remaining within the total cabinet height.");

    var topRailAuxiliary = layout.Parts.Single(part => part.Name == "顶线辅助板");
    Assert(topRailAuxiliary.Box.MinX == 0 && topRailAuxiliary.Box.MaxX == 900 &&
           topRailAuxiliary.Box.MinY == 2050 && topRailAuxiliary.Box.MaxY == 2068 &&
           topRailAuxiliary.Box.MinZ == 18 && topRailAuxiliary.Box.MaxZ == 88 &&
           topRailAuxiliary.CutWidth == 900 && topRailAuxiliary.CutHeight == 70 &&
           topRailAuxiliary.CutThickness == 18,
        "Top-rail auxiliary board must start behind the face and extend rearward as an L section.");

    var top = layout.Parts.Single(part => part.Kind == CabinetPartKind.Top);
    var leftSide = layout.Parts.Single(part => part.Kind == CabinetPartKind.LeftSide);
    Assert(top.Box.MinY == 2032 && top.Box.MaxY == 2050 &&
           leftSide.Box.MaxY == 2050 && leftSide.CutWidth == 2050,
        "Cabinet body height must exclude the 50 mm top rail from the confirmed total height.");

    var elevationShapes = layout.Shapes.Where(shape => shape.View == CabinetViewKind.Elevation).ToArray();
    Assert(elevationShapes.All(shape => !shape.Hidden) &&
           elevationShapes.All(shape => shape.Part is not CabinetPartKind.Backboard and not CabinetPartKind.RearToeKick),
        "正视图仍显示背板、后踢脚或大型虚线外框。" );

    var elevationTop = elevationShapes.Single(shape => shape.Part == CabinetPartKind.Top);
    var elevationTopRail = elevationShapes.Single(shape => shape.Part == CabinetPartKind.TopRail);
    Assert(elevationTopRail.Bounds.Bottom == elevationTop.Bounds.Top &&
           elevationTopRail.Bounds.Top == layout.ElevationBounds.Top,
        "Front-view top rail must be directly above the top board within the total height.");

    var planShapes = layout.Shapes.Where(shape => shape.View == CabinetViewKind.Plan).ToArray();
    var planHidden = planShapes.Where(shape => shape.Hidden).ToArray();
    Assert(planHidden is [{ Part: CabinetPartKind.Backboard }] &&
           Math.Abs(planHidden[0].Bounds.Top - layout.PlanBounds.Top) < 0.000001 &&
           planShapes.All(shape => shape.Part is not CabinetPartKind.Bottom and
                                             not CabinetPartKind.FrontToeKick and
                                             not CabinetPartKind.RearToeKick),
        "俯视图没有把唯一的背板虚线放在上方，或仍含底板/踢脚重复矩形。" );

    var sideShapes = layout.Shapes.Where(shape => shape.View == CabinetViewKind.Side).ToArray();
    var sideBackboard = sideShapes.Single(shape => shape.Part == CabinetPartKind.Backboard);
    var sideTop = sideShapes.Single(shape => shape.Part == CabinetPartKind.Top);
    var sideBottom = sideShapes.Single(shape => shape.Part == CabinetPartKind.Bottom);
    var sideTopRail = sideShapes.Single(shape => shape.Part == CabinetPartKind.TopRail);
    var sideTopRailAuxiliary = sideShapes.Single(shape => shape.Part == topRailAuxiliary.Kind);
    Assert(sideBackboard.Hidden &&
           Math.Abs(sideBackboard.Bounds.Left - layout.SideBounds.Left) < 0.000001 &&
           sideBackboard.Bounds.Bottom == sideBottom.Bounds.Top &&
           sideBackboard.Bounds.Top == sideTop.Bounds.Bottom &&
           Math.Abs(layout.SideBounds.Bottom - layout.ElevationBounds.Bottom) < 0.000001 &&
           Math.Abs(layout.SideBounds.Top - layout.ElevationBounds.Top) < 0.000001 &&
           sideShapes.All(shape => shape.Part != CabinetPartKind.RightSide) &&
           sideShapes.Where(shape => shape.Hidden)
               .All(shape => shape.Part is CabinetPartKind.Backboard or CabinetPartKind.RearToeKick),
        "侧视图没有把后方背板放在左侧，或仍投影重叠侧板形成大型虚线框。" );

    Assert(sideTopRailAuxiliary.Bounds.Right == sideTopRail.Bounds.Left &&
           sideTopRailAuxiliary.Bounds.Bottom == sideTopRail.Bounds.Bottom &&
           sideTopRailAuxiliary.Bounds.Height == 18,
        "Side-view top-rail auxiliary board must touch the rear face without overlapping it.");

    var planBackboard = planShapes.Single(shape => shape.Part == CabinetPartKind.Backboard);
    Assert(planBackboard.Bounds.Left >= layout.PlanBounds.Left &&
           planBackboard.Bounds.Right <= layout.PlanBounds.Right,
        "Plan backboard must not project beyond the left and right side panels.");

    var planAndSide = CabinetLayoutCalculator.Calculate(options with { GenerateElevation = false });
    Assert(planAndSide.PlanBounds.Left == 0 && planAndSide.PlanBounds.Bottom == 0 &&
           Math.Abs(planAndSide.SideBounds.Left - planAndSide.PlanBounds.Right - 400) < 0.000001,
        "关闭正视图后，俯视图没有成为放置基准，或侧视图仍相对不可见正视图定位。" );
    var sideOnly = CabinetLayoutCalculator.Calculate(options with
    {
        GenerateElevation = false,
        GeneratePlan = false
    });
    Assert(sideOnly.SideBounds.Left == 0 && sideOnly.SideBounds.Bottom == 0,
        "只生成侧视图时仍然保留了不可见视图的空白偏移。" );
}

static void PlanCabinetOptionsUseConfirmedDefaults()
{
    var options = PlanCabinetOptions.Default;

    Assert(options.Length == 1800 && options.Depth == 550 && options.Thickness == 18 &&
           options.GenerateDoors && options.DoorThickness == 18 && options.DoorGap == 0 &&
           options.DoorFrontClearance == 2 && options.DoorCount == 2 &&
           options.DoorCalculationMode == DoorPanelCalculationMode.GapRoundedDoorDistributed &&
           options.GenerateMiddlePanels && !options.MiddlePanelSameDepthAsSide &&
           options.MiddlePanelMode == PlanCabinetMiddlePanelMode.AlignToDoorGaps &&
           options.TopFrontInset == 0 && options.TopRearInset == 0 &&
           !options.BackboardRearCover && options.BackboardThickness == 8 &&
           options.BackboardInset == 12 && options.BackboardLeftGroove == 5 &&
           options.BackboardRightGroove == 5 && options.TryValidate(out _),
        "独立柜体俯视图默认尺寸、门板、中竖板或背板参数与确认界面不一致。" );

    Assert(!(options with { Length = 30 }).TryValidate(out _),
        "独立柜体俯视图错误地接受了小于两块侧板厚度的柜长。" );
    Assert(!(options with { TopFrontInset = 300, TopRearInset = 300 }).TryValidate(out _),
        "独立柜体俯视图错误地接受了超过柜深的顶板缩进。" );
    Assert(!(options with { BackboardInset = 550 }).TryValidate(out _),
        "独立柜体俯视图错误地接受了无法放入柜深的背板。" );
}

static void PlanCabinetLayoutTracksDoorCount()
{
    var options = PlanCabinetOptions.Default;
    var layout = PlanCabinetLayoutCalculator.Calculate(options);
    var doors = layout.Shapes.Where(shape => shape.Kind == PlanCabinetPartKind.Door).ToArray();
    var middlePanels = layout.Shapes.Where(shape => shape.Kind == PlanCabinetPartKind.MiddlePanel).ToArray();
    var backboard = layout.Shapes.Single(shape => shape.Kind == PlanCabinetPartKind.Backboard);

    Assert(layout.Shapes.Count == 7 && doors.Length == 2 && middlePanels.Length == 1 &&
           layout.ActualDoorGap == 0,
        "默认俯视图没有生成两扇门、一块中竖板及完整柜体结构。" );
    Assert(doors[0].Bounds.Left == 0 && doors[0].Bounds.Right == 900 &&
           doors[1].Bounds.Left == 900 && doors[1].Bounds.Right == 1800 &&
           doors.All(door => door.Bounds.Bottom == -20 && door.Bounds.Top == -2),
        "门板没有位于柜体前方，或门板左右边界未覆盖确认的柜长。" );
    var twoMillimeterGap = PlanCabinetLayoutCalculator.Calculate(options with { DoorGap = 2 });
    var gapDoors = twoMillimeterGap.Shapes
        .Where(shape => shape.Kind == PlanCabinetPartKind.Door)
        .ToArray();
    var gapMiddle = twoMillimeterGap.Shapes
        .Single(shape => shape.Kind == PlanCabinetPartKind.MiddlePanel);
    Assert(twoMillimeterGap.ActualDoorGap == 2 &&
           gapDoors[0].Bounds.Left == 2 && gapDoors[0].Bounds.Right == 899 &&
           gapDoors[1].Bounds.Left == 901 && gapDoors[1].Bounds.Right == 1798 &&
           gapDoors.All(door => door.CutWidth == 897) &&
           (gapMiddle.Bounds.Left + gapMiddle.Bounds.Right) / 2 == 900,
        "柜体俯视图门缝必须同时扣除左边缝、中间缝和右边缝。" );

    Assert(Math.Abs((middlePanels[0].Bounds.Left + middlePanels[0].Bounds.Right) / 2 - 900) < 0.000001 &&
           middlePanels[0].Bounds.Bottom == 0 && middlePanels[0].Bounds.Top == 550,
        "中竖板没有按门缝中线定位，或默认深度未跟随顶板范围。" );
    Assert(backboard.Hidden && backboard.Bounds.Left == 13 && backboard.Bounds.Right == 1787 &&
           backboard.Bounds.Bottom == 530 && backboard.Bounds.Top == 538,
        "槽装背板没有按12后缩、8厚度及左右5插槽生成虚线矩形。" );

    var fourDoorLayout = PlanCabinetLayoutCalculator.Calculate(options with { DoorCount = 4 });
    Assert(fourDoorLayout.Shapes.Count(shape => shape.Kind == PlanCabinetPartKind.Door) == 4 &&
           fourDoorLayout.Shapes.Count(shape => shape.Kind == PlanCabinetPartKind.MiddlePanel) == 3,
        "门数调整后，门板与中竖板数量没有同步联动。" );

    var distributed = PlanCabinetLayoutCalculator.Calculate(options with
    {
        DoorCount = 3,
        DoorLeftExtension = 30,
        DoorRightExtension = 10,
        MiddlePanelMode = PlanCabinetMiddlePanelMode.Distributed
    });
    var distributedMiddles = distributed.Shapes
        .Where(shape => shape.Kind == PlanCabinetPartKind.MiddlePanel)
        .ToArray();
    Assert(distributedMiddles.Length == 2 &&
           Math.Abs((distributedMiddles[0].Bounds.Left + distributedMiddles[0].Bounds.Right) / 2 - 606) < 0.000001 &&
           Math.Abs((distributedMiddles[1].Bounds.Left + distributedMiddles[1].Bounds.Right) / 2 - 1194) < 0.000001,
        "均分柜体模式不应受门板左右延伸影响。" );
}

static void PlanCabinetSurfaceCommandsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var xaml = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PlanCabinetCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    Assert(xaml.Contains("Header=\"俯视图\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"PlanCabinetLengthBox\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"PlanCabinetDoorModeCombo\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"PlanCabinetMiddleModeCombo\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"生成俯视图 (DCFST)\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"俯视图门板替换 (DCMBT2)\"", StringComparison.Ordinal),
        "柜体俯视图参数页或两个操作按钮不完整。" );
    Assert(panel.Contains("TryReadPlanCabinetOptions", StringComparison.Ordinal) &&
           panel.Contains("PlanCabinetRuntimeSettings.Save", StringComparison.Ordinal) &&
           panel.Contains("DC_CMD_PLAN_CABINET_GENERATE", StringComparison.Ordinal) &&
           panel.Contains("DC_CMD_PLAN_CABINET_REPLACE_DOORS", StringComparison.Ordinal),
        "柜体俯视图面板没有统一读取、保存或调用两个底层命令。" );
    Assert(commands.Contains("DC_CMD_PLAN_CABINET_GENERATE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_PLAN_CABINET_REPLACE_DOORS", StringComparison.Ordinal) &&
           commands.Contains("\"DCFST\"", StringComparison.Ordinal) &&
           commands.Contains("\"DCMBT2\"", StringComparison.Ordinal) &&
           commands.Contains("PlanCabinetLayoutCalculator.Calculate", StringComparison.Ordinal) &&
           commands.Contains("PlanCabinetPartKind.Door", StringComparison.Ordinal),
        "DCFST/DCMBT2命令、布局计算或只替换门板的入口不完整。" );
    Assert(!commands.Contains("UserInputControls.GovernedByOrthoMode", StringComparison.Ordinal),
        "Plan cabinet placement must follow both mouse axes instead of inheriting ORTHO mode.");
    Assert(application.Contains(
            "CommandClass(typeof(YiZhiMao.Dc.Plugin.PlanCabinetCommands))",
            StringComparison.Ordinal),
        "柜体俯视图命令类未注册到插件。" );
}

static void CabinetSurfaceCommandsAndStatisticsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var xaml = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "CabinetCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));
    var statistics = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "StatisticsCommands.cs"));

    Assert(xaml.IndexOf("x:Name=\"CabinetModuleButton\"", StringComparison.Ordinal) <
           xaml.IndexOf("x:Name=\"BoardModuleButton\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"CabinetModuleView\"", StringComparison.Ordinal) &&
           xaml.Contains("Header=\"标准单元柜\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"CabinetGenerateElevationCheck\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"CabinetGeneratePlanCheck\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"CabinetGenerateSideCheck\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"CabinetTopRailAuxiliaryDepthBox\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"一键生成单元柜 (DCDG)\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"全局刷新柜体/板件 (DCXX)\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"生成万能柜体 (DCWNG)\"", StringComparison.Ordinal),
        "柜体导航、标准单元柜参数或四个操作按钮不完整。" );
    Assert(panel.Contains("TryReadCabinetOptions", StringComparison.Ordinal) &&
           panel.Contains("CabinetRuntimeSettings.Save", StringComparison.Ordinal) &&
           panel.Contains("CabinetOptions.Default", StringComparison.Ordinal),
        "柜体面板没有统一读取、保存或重置参数。" );
    Assert(commands.Contains("DC_CMD_CABINET_STANDARD", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_CABINET_REFRESH", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_CABINET_UNIVERSAL", StringComparison.Ordinal) &&
           commands.Contains("YIZHIMAO_DC_CABINET_OPTIONS", StringComparison.Ordinal) &&
           commands.Contains("BoardCommands.SetBoardProperty", StringComparison.Ordinal) &&
           commands.Contains("CabinetLayoutCalculator.Calculate", StringComparison.Ordinal),
        "DCDG、DCXX、DCWNG 或柜体刷新/拆单元数据入口不完整。" );
    var horizontalMode = commands.IndexOf(
        "HorizontalMode = TextHorizontalMode.TextCenter",
        StringComparison.Ordinal);
    var verticalMode = commands.IndexOf(
        "VerticalMode = TextVerticalMode.TextVerticalMid",
        StringComparison.Ordinal);
    var alignmentPoint = commands.IndexOf("AlignmentPoint = labelPoint", StringComparison.Ordinal);
    Assert(horizontalMode >= 0 && verticalMode >= 0 &&
           alignmentPoint > horizontalMode && alignmentPoint > verticalMode,
        "柜体说明文字在设置居中模式前写入 AlignmentPoint，会触发 AutoCAD eNotApplicable。" );
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.CabinetCommands))", StringComparison.Ordinal) &&
           statistics.Contains("HasExplicitSize", StringComparison.Ordinal) &&
           statistics.Contains("柜体辅助图", StringComparison.Ordinal),
        "柜体命令未注册，或辅助视图仍可能重复计入拆单。" );
}

static void ToolboxSettingsPersistAcrossModules()
{
    var root = Path.Combine(Path.GetTempPath(), $"dc-toolbox-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        var path = Path.Combine(root, "ToolboxSettings.json");
        var store = new ToolboxSettingsStore(path);
        var settings = ToolboxSettings.CreateDefault();
        settings.DoorPanel = settings.DoorPanel with { TopExtension = 55, GenerateMask = true };
        settings.PlanDoorPanel = settings.PlanDoorPanel with { Thickness = 20, FrontClearance = 3 };
        settings.DrawerFace = settings.DrawerFace with { Count = 4, GenerateMask = true };
        settings.Hardware = settings.Hardware with { HingeTopOffset = 88, HingeTypeTextHeight = 40 };
        settings.Grille = settings.Grille with { StripGap = 26 };
        settings.PerforatedPanel = settings.PerforatedPanel with { HoleDiameter = 12 };
        settings.BackgroundWall = settings.BackgroundWall with
        {
            GenerateAllPlanRows = true,
            Rows = [new BackgroundWallRowOptions(3, 650), new BackgroundWallRowOptions(4, 720)]
        };
        settings.Board = settings.Board with
        {
            Thickness = 20,
            DisplaceDistance = 135,
            DivideCount = 4,
            DivideThickness = 20,
            DivideDepth = 120,
            DivideMaximumClearSpan = 450,
            DivideSmartMode = false,
            DivideRemainderAtEnd = false,
            DoorPartitionTopExtension = 2800,
            DoorPartitionBottomExtension = 350,
            DoorPartitionMaximumWidth = 500,
            DoorPartitionDefaultCount = 7,
            DoorPartitionSmartMode = false,
            ShoeShelfDepth = 280,
            ShoeShelfSmartAngle = false,
            ShoeShelfAngleDegrees = 25,
            ShoeShelfTargetSpacing = 210,
            ShoeShelfTopReserve = 15,
            ShoeShelfBottomReserve = 30,
            ShoeShelfFrontClearance = 25,
            ShoeShelfRearClearance = 5,
            ShoeShelfStopperHeight = 22,
            ShoeShelfGenerateStopper = false,
            PropertyType = "见光板",
            TrimFaceWidth = 55,
            TrimAuxiliaryDepth = 75,
            SideTrimSectionType = "T",
            TopTrimSectionType = "I",
            PlacementView = "平面"
        };
        settings.DrawingLayer = settings.DrawingLayer with { LinetypeScale = 50, WallHatchScale = 2 };
        settings.SmartAnnotation = settings.SmartAnnotation with
        {
            TemplateName = "雷神标注样式.dwg",
            InnerOffset = 180,
            FilterThickness = 20,
            FilterText = false,
            FilterBlocks = true
        };
        settings.ImageBatch = settings.ImageBatch with { Gap = 80, PixelHeight = 1200 };
        settings.Cabinet = settings.Cabinet with
        {
            Width = 2200,
            Thickness = 20,
            GenerateSide = false,
            PlanViewSpacing = 350
        };
        settings.PlanCabinet = settings.PlanCabinet with
        {
            Length = 2000,
            DoorCount = 4,
            DoorGap = 3,
            BackboardRearCover = true
        };
        settings.WholeHouseStatistics = settings.WholeHouseStatistics with
        {
            IncludeLightStrips = false,
            TableScaleFactor = 10,
            MaxRowsPerPage = 24
        };
        settings.DoorStatistics = settings.DoorStatistics with { Thickness = 20, MaxRowsPerPage = 26 };
        settings.FrameDoorStatistics = settings.FrameDoorStatistics with
        {
            GlassCoreCount = 1,
            CenterMiddleFrame = false,
            LowerFramePosition = 650
        };
        settings.CabinetBom = settings.CabinetBom with { IncludeCabinetSummary = false, TableScaleFactor = 9 };
        settings.PanelNesting = settings.PanelNesting with
        {
            UseWoodGrain = false,
            UseSolidColor = true,
            Use48Sheet = false,
            Use49Sheet = true,
            IncludeFlatDoors = false
        };
        settings.DefaultSaveFileFormat = DrawingSaveFileFormat.AutoCad2010;

        store.Save(settings);
        var loaded = store.Load();

        Assert(loaded.DoorPanel.TopExtension == 55 && loaded.DoorPanel.GenerateMask,
            "立面门板参数没有从统一配置恢复。" );
        Assert(loaded.PlanDoorPanel.Thickness == 20 && loaded.DrawerFace.Count == 4,
            "俯视门板或抽面参数没有从统一配置恢复。" );
        Assert(loaded.Hardware.HingeTopOffset == 88 && loaded.Hardware.HingeTypeTextHeight == 40,
            "五金参数没有从统一配置恢复。" );
        Assert(loaded.Grille.StripGap == 26 && loaded.PerforatedPanel.HoleDiameter == 12 &&
               loaded.BackgroundWall.Rows.Count == 2 && loaded.BackgroundWall.GenerateAllPlanRows,
            "墙板参数没有从统一配置恢复。" );
        Assert(loaded.Board.Thickness == 20 && loaded.Board.DisplaceDistance == 135 && loaded.Board.DivideCount == 4 &&
               loaded.Board.DivideDepth == 120 && loaded.Board.DivideMaximumClearSpan == 450 &&
               !loaded.Board.DivideSmartMode && !loaded.Board.DivideRemainderAtEnd &&
               loaded.Board.DoorPartitionTopExtension == 2800 &&
               loaded.Board.DoorPartitionBottomExtension == 350 &&
               loaded.Board.DoorPartitionMaximumWidth == 500 &&
               loaded.Board.DoorPartitionDefaultCount == 7 &&
               !loaded.Board.DoorPartitionSmartMode &&
               loaded.Board.ShoeShelfDepth == 280 && !loaded.Board.ShoeShelfSmartAngle &&
               loaded.Board.ShoeShelfAngleDegrees == 25 && loaded.Board.ShoeShelfTargetSpacing == 210 &&
               loaded.Board.ShoeShelfTopReserve == 15 && loaded.Board.ShoeShelfBottomReserve == 30 &&
               loaded.Board.ShoeShelfFrontClearance == 25 && loaded.Board.ShoeShelfRearClearance == 5 &&
               loaded.Board.ShoeShelfStopperHeight == 22 && !loaded.Board.ShoeShelfGenerateStopper &&
               loaded.Board.PropertyType == "见光板" &&
               loaded.Board.TrimFaceWidth == 55 && loaded.Board.TrimAuxiliaryDepth == 75 &&
               loaded.Board.SideTrimSectionType == "T" && loaded.Board.TopTrimSectionType == "I" &&
               loaded.Board.PlacementView == "平面" &&
               loaded.DrawingLayer.LinetypeScale == 50 && loaded.ImageBatch.Gap == 80 &&
               loaded.SmartAnnotation.TemplateName == "雷神标注样式.dwg" &&
               loaded.SmartAnnotation.InnerOffset == 180 && loaded.SmartAnnotation.FilterThickness == 20 &&
               !loaded.SmartAnnotation.FilterText && loaded.SmartAnnotation.FilterBlocks,
            "板件、图层、智能标注或批量图片参数没有从统一配置恢复。" );
        Assert(loaded.DefaultSaveFileFormat == DrawingSaveFileFormat.AutoCad2010,
            "默认保存版本没有从统一配置恢复。" );
        Assert(loaded.Cabinet.Width == 2200 && loaded.Cabinet.Thickness == 20 &&
               !loaded.Cabinet.GenerateSide && loaded.Cabinet.PlanViewSpacing == 350,
            "标准单元柜参数没有从统一配置恢复。" );
        Assert(loaded.PlanCabinet.Length == 2000 && loaded.PlanCabinet.DoorCount == 4 &&
               loaded.PlanCabinet.DoorGap == 3 && loaded.PlanCabinet.BackboardRearCover,
            "独立柜体俯视图参数没有从统一配置恢复。" );
        Assert(!loaded.WholeHouseStatistics.IncludeLightStrips &&
               loaded.WholeHouseStatistics.TableScaleFactor == 10 &&
               loaded.WholeHouseStatistics.MaxRowsPerPage == 24,
            "全屋拆单项目配置或表格参数没有从统一配置恢复。" );
        Assert(loaded.DoorStatistics.Thickness == 20 && loaded.DoorStatistics.MaxRowsPerPage == 26 &&
               loaded.FrameDoorStatistics.GlassCoreCount == 1 &&
               !loaded.FrameDoorStatistics.CenterMiddleFrame &&
               loaded.FrameDoorStatistics.LowerFramePosition == 650 &&
               !loaded.CabinetBom.IncludeCabinetSummary && loaded.CabinetBom.TableScaleFactor == 9 &&
               loaded.PanelNesting.UseSolidColor && loaded.PanelNesting.Use49Sheet &&
               !loaded.PanelNesting.IncludeFlatDoors,
            "门板、边框门、柜体或板件高级拆单参数没有从统一配置恢复。" );
        Assert(!File.Exists(path + ".tmp"), "原子保存后遗留了临时配置文件。" );
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void ToolboxSettingsFallbackOnInvalidFiles()
{
    var root = Path.Combine(Path.GetTempPath(), $"dc-toolbox-invalid-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        var path = Path.Combine(root, "ToolboxSettings.json");
        var store = new ToolboxSettingsStore(path);
        File.WriteAllText(path, "{ invalid json");
        Assert(store.Load().Board == BoardToolOptions.Default,
            "损坏 JSON 没有回退工具箱默认参数。" );

        var invalidSideT = ToolboxSettings.CreateDefault();
        invalidSideT.Board = invalidSideT.Board with
        {
            Thickness = 18,
            TrimFaceWidth = 17,
            SideTrimSectionType = "T"
        };
        AssertThrows<InvalidOperationException>(
            invalidSideT.Validate,
            "Toolbox settings should reject a side T profile narrower than its thickness.");

        var invalidTopT = ToolboxSettings.CreateDefault();
        invalidTopT.Board = invalidTopT.Board with
        {
            Thickness = 18,
            TrimFaceWidth = 17,
            TopTrimSectionType = "T"
        };
        AssertThrows<InvalidOperationException>(
            invalidTopT.Validate,
            "Toolbox settings should reject a top T profile narrower than its thickness.");

        var narrowNonT = ToolboxSettings.CreateDefault();
        narrowNonT.Board = narrowNonT.Board with
        {
            Thickness = 18,
            TrimFaceWidth = 17,
            SideTrimSectionType = "L",
            TopTrimSectionType = "I"
        };
        narrowNonT.Validate();

        File.WriteAllText(path, "{\"Board\":{\"Thickness\":-1}}");
        Assert(store.Load().Board == BoardToolOptions.Default,
            "非法业务参数没有整体回退工具箱默认参数。" );
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void ToolboxRuntimeUsesSharedPersistentSettings()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var runtime = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "ToolboxSettingsRuntime.cs"));
    var doorRuntime = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelRuntimeSettings.cs"));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var board = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "BoardCommands.cs"));

    Assert(runtime.Contains("ToolboxSettings.json", StringComparison.Ordinal) &&
           runtime.Contains("ToolboxSettingsStore", StringComparison.Ordinal) &&
           runtime.Contains("SetInMemory", StringComparison.Ordinal) &&
           runtime.Contains("Update", StringComparison.Ordinal),
        "插件没有建立统一的工具箱参数持久化运行时。" );
    Assert(doorRuntime.Contains("set => ToolboxSettingsRuntime.SetInMemory", StringComparison.Ordinal) &&
           doorRuntime.Contains("ToolboxSettingsRuntime.Update", StringComparison.Ordinal),
        "实时预览参数和显式保存没有分离，可能在 W/S 调整时频繁写盘。" );
    Assert(panel.Contains("LoadDrawingLayerModule();", StringComparison.Ordinal) &&
           panel.Contains("BoardRuntimeSettings.Save(", StringComparison.Ordinal) &&
           panel.Contains("DrawingLayerRuntimeSettings.Save(", StringComparison.Ordinal),
        "面板没有从统一运行时加载并保存板件/图层参数。" );
    Assert(board.Contains("ToolboxSettingsRuntime.Settings.Board", StringComparison.Ordinal),
        "直接输入板件命令仍未读取统一持久化参数。" );
}

static void ToolboxPreferencesPersistAndKeepOneModule()
{
    var defaults = ToolboxPreferenceOptions.Default;
    var smartInputMethodDefaults = SmartInputMethodOptions.Default;
    Assert(defaults.AutoExpandSidebar &&
           defaults.EnabledModuleCount == ToolboxFeatureModuleCatalog.All.Count &&
           ToolboxFeatureModuleCatalog.All.Count == 15,
        "偏好设置默认值没有自动展开全部十五个功能模块。" );
    Assert(smartInputMethodDefaults.Enabled &&
           smartInputMethodDefaults.KeepCadCommandsEnglish &&
           smartInputMethodDefaults.AutoChineseForText &&
           smartInputMethodDefaults.RestoreEnglishAfterText,
        "智能输入法默认设置没有全部启用。" );
    Assert(ToolboxSettings.CreateDefault().DefaultSaveFileFormat == DrawingSaveFileFormat.AutoCad2007,
        "工具箱首次使用没有默认采用 AutoCAD 2007 DWG 保存格式。" );
    Assert(!ToolboxSettings.CreateDefault().SyncReferencedShxOnSave,
        "工具箱首次使用不应默认开启 SHX 自动同步。" );
    Assert(ToolboxSettings.CreateDefault().AutoReplaceMissingShx,
        "工具箱首次使用应默认开启缺失文字 SHX 自动替换。" );
    Assert(ToolboxFeatureModuleCatalog.All.Select(item => item.Module).Distinct().Count() == 15,
        "偏好设置模块目录存在缺项或重复项。" );

    var root = Path.Combine(Path.GetTempPath(), $"dc-preferences-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        var path = Path.Combine(root, "ToolboxSettings.json");
        var store = new ToolboxSettingsStore(path);
        var settings = ToolboxSettings.CreateDefault();
        settings.Preferences = settings.Preferences with { AutoExpandSidebar = false };
        settings.Preferences = settings.Preferences.WithModuleEnabled(ToolboxFeatureModule.Boards, false);
        settings.Preferences = settings.Preferences.WithModuleEnabled(ToolboxFeatureModule.SuperLibrary, false);
        settings.SmartInputMethod = settings.SmartInputMethod with { AutoChineseForText = false };
        settings.SyncReferencedShxOnSave = true;
        settings.AutoReplaceMissingShx = false;
        settings.MissingShxReplacementMappings["TextFont|lost.shx"] = "simplex.shx";
        store.Save(settings);

        var loaded = store.Load();
        Assert(!loaded.Preferences.AutoExpandSidebar &&
               !loaded.Preferences.IsEnabled(ToolboxFeatureModule.Boards) &&
               !loaded.Preferences.IsEnabled(ToolboxFeatureModule.SuperLibrary) &&
               loaded.Preferences.IsEnabled(ToolboxFeatureModule.DoorPanels) &&
               !loaded.SmartInputMethod.AutoChineseForText &&
               loaded.SmartInputMethod.Enabled &&
               loaded.SyncReferencedShxOnSave &&
               !loaded.AutoReplaceMissingShx &&
               loaded.MissingShxReplacementMappings["TextFont|lost.shx"] == "simplex.shx",
            "偏好设置没有跨启动持久化自动展开或模块开关。" );

        var noneEnabled = ToolboxPreferenceOptions.Default;
        foreach (var info in ToolboxFeatureModuleCatalog.All)
        {
            noneEnabled = noneEnabled.WithModuleEnabled(info.Module, false);
        }

        AssertThrows<InvalidOperationException>(
            () => new ToolboxSettings { Preferences = noneEnabled }.Validate(),
            "偏好设置允许关闭全部功能模块。" );
        AssertThrows<InvalidOperationException>(
            () => new ToolboxSettings { DefaultSaveFileFormat = (DrawingSaveFileFormat)999 }.Validate(),
            "偏好设置允许保存不受支持的 DWG 版本。" );

        File.WriteAllText(path, "{}");
        var compatibleDefaults = store.Load();
        Assert(compatibleDefaults.Preferences.EnabledModuleCount == 15 &&
               compatibleDefaults.Preferences.IsEnabled(ToolboxFeatureModule.SuperLibrary) &&
               compatibleDefaults.SmartInputMethod == SmartInputMethodOptions.Default &&
               compatibleDefaults.DefaultSaveFileFormat == DrawingSaveFileFormat.AutoCad2007 &&
               !compatibleDefaults.SyncReferencedShxOnSave &&
               compatibleDefaults.AutoReplaceMissingShx,
            "旧版工具箱配置缺少偏好字段时没有兼容恢复默认值。" );
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void ToolboxPreferencesSurfaceAndGuardAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var xaml = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var panelCode = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var startup = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));
    var guard = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "ModulePreferencesRuntime.cs"));
    var palette = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ToolboxPalette.cs"));
    var smartInputMethod = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "SmartInputMethodRuntime.cs"));
    var cadQuickToggle = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "CadQuickToggleRuntime.cs"));
    var pointStyleIcon = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "PointStyleIcon.cs"));
    var shxRuntime = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "ShxFontSyncRuntime.cs"));
    var missingShxRuntime = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "MissingShxReplacementRuntime.cs"));

    Assert(xaml.Contains("x:Name=\"PreferencesModuleButton\"", StringComparison.Ordinal) &&
           xaml.Contains("Text=\"偏好设置\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"启动时自动展开侧边栏\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"全部显示\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"ModulePreferenceItemsControl\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"PinnedPreferencesCheckBox\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"SmartInputMethodEnabledCheckBox\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"KeepCadCommandsEnglishCheckBox\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"AutoChineseForTextCheckBox\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"RestoreEnglishAfterTextCheckBox\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"CadQuickToggleExpander\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"PointerInputCheckBox\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"指针输入（P）\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"PointStyleListBox\"", StringComparison.Ordinal) &&
           !xaml.Contains("x:Name=\"PointStyleComboBox\"", StringComparison.Ordinal) &&
           xaml.Contains("SelectionChanged=\"OnPointStyleChanged\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"SaveFileFormatComboBox\"", StringComparison.Ordinal) &&
           xaml.Contains("SelectionChanged=\"OnSaveFileFormatChanged\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"SyncReferencedShxOnSaveCheckBox\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"AutoReplaceMissingShxCheckBox\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"ExportCurrentShxButton\"", StringComparison.Ordinal) &&
           xaml.Contains("<UniformGrid Columns=\"5\"", StringComparison.Ordinal) &&
           xaml.Contains("IsExpanded=\"False\"", StringComparison.Ordinal) &&
           xaml.Contains("Expanded=\"OnCadQuickToggleExpanded\"", StringComparison.Ordinal) &&
           xaml.Contains("IsChecked=\"True\"", StringComparison.Ordinal) &&
           xaml.Contains("IsEnabled=\"False\"", StringComparison.Ordinal),
        "偏好设置入口、常规设置或模块列表不完整。" );
    Assert(panelCode.Contains("ApplyModuleVisibility", StringComparison.Ordinal) &&
           panelCode.Contains("EnabledModuleCount == 0", StringComparison.Ordinal) &&
           panelCode.Contains("ToolboxSettingsRuntime.Update", StringComparison.Ordinal) &&
           panelCode.Contains("SmartInputMethodRuntime.RefreshSettings()", StringComparison.Ordinal) &&
           panelCode.Contains("private void OnCadQuickToggleExpanded", StringComparison.Ordinal) &&
           panelCode.Contains("CadQuickToggleRuntime.TryGetPointerInputEnabled", StringComparison.Ordinal) &&
           panelCode.Contains("CadQuickToggleRuntime.TrySetPointerInputEnabled", StringComparison.Ordinal) &&
           panelCode.Contains("DefaultPointStyleMode = 35", StringComparison.Ordinal) &&
           panelCode.Contains("PointStyleListBox.ItemsSource = PointStyleOptions", StringComparison.Ordinal) &&
           panelCode.Contains("private void OnPointStyleChanged", StringComparison.Ordinal) &&
           panelCode.Contains("CadQuickToggleRuntime.TryGetPointStyle", StringComparison.Ordinal) &&
           panelCode.Contains("CadQuickToggleRuntime.TrySetPointStyle", StringComparison.Ordinal) &&
           panelCode.Contains("private void OnSaveFileFormatChanged", StringComparison.Ordinal) &&
           panelCode.Contains("CadQuickToggleRuntime.TrySetSaveFileFormat", StringComparison.Ordinal) &&
           panelCode.Contains("settings.DefaultSaveFileFormat = format", StringComparison.Ordinal) &&
           panelCode.Contains("private void OnSyncReferencedShxOnSaveChanged", StringComparison.Ordinal) &&
           panelCode.Contains("private void OnAutoReplaceMissingShxChanged", StringComparison.Ordinal) &&
           panelCode.Contains("MissingShxReplacementRuntime.QueueActiveDocument", StringComparison.Ordinal) &&
           panelCode.Contains("ShxFontSyncRuntime.ExportActiveDocumentAsync", StringComparison.Ordinal),
        "模块入口没有即时隐藏、最后一项保护或持久化逻辑。" );
    Assert(startup.Contains("ScheduleAutoExpand", StringComparison.Ordinal) &&
           startup.Contains("ToolboxPalette.Show()", StringComparison.Ordinal) &&
           startup.Contains("ModuleCommandGuard.Initialize()", StringComparison.Ordinal) &&
           startup.Contains("SmartInputMethodRuntime.Initialize()", StringComparison.Ordinal) &&
           startup.Contains("SmartInputMethodRuntime.Dispose()", StringComparison.Ordinal) &&
           startup.Contains("ShxFontSyncRuntime.Initialize()", StringComparison.Ordinal) &&
           startup.Contains("ShxFontSyncRuntime.Dispose()", StringComparison.Ordinal) &&
           startup.Contains("MissingShxReplacementRuntime.Initialize()", StringComparison.Ordinal) &&
           startup.Contains("MissingShxReplacementRuntime.Dispose()", StringComparison.Ordinal) &&
           startup.Contains("CadQuickToggleRuntime.TryApplyConfiguredSaveFileFormat", StringComparison.Ordinal),
        "启动自动展开或命令禁用守卫没有注册。" );
    var immediateExpandCall = startup.IndexOf("TryAutoExpandImmediately();", StringComparison.Ordinal);
    var saveFormatCall = startup.IndexOf("CadQuickToggleRuntime.TryApplyConfiguredSaveFileFormat", StringComparison.Ordinal);
    Assert(immediateExpandCall >= 0 && immediateExpandCall < saveFormatCall &&
           startup.Contains("catch (System.Exception)", StringComparison.Ordinal) &&
           startup.Contains("ScheduleAutoExpand();", StringComparison.Ordinal),
        "工具箱仍等待 AutoCAD Idle 才显示，会被缺少 SHX 等启动模态窗口阻塞。" );
    Assert(shxRuntime.Contains("SaveComplete", StringComparison.Ordinal) &&
           shxRuntime.Contains("TextStyleTable", StringComparison.Ordinal) &&
           shxRuntime.Contains("BigFontFileName", StringComparison.Ordinal) &&
           shxRuntime.Contains("ShapeStyleAt", StringComparison.Ordinal) &&
           shxRuntime.Contains("Task.Run", StringComparison.Ordinal) &&
           shxRuntime.Contains("\"Modules\", \"Fonts\"", StringComparison.Ordinal) &&
           shxRuntime.Contains("\".dwg\"", StringComparison.Ordinal),
        "SHX 同步没有限定正式 DWG 保存、引用样式或后台文件复制。" );
    Assert(missingShxRuntime.Contains("HostApplicationServices.Current.FindFile", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("ShapeStyleAt", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("MissingShxReplacementPolicy.Decide", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("TextStyleTableRecord", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("BigFontFileName", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("Path.Combine(fontLibrary", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("Editor.WriteMessage", StringComparison.Ordinal),
        "缺失 SHX 运行时没有延迟扫描文字样式、保护形文件或输出非模态结果。" );
    Assert(missingShxRuntime.Contains("private static void TryQueue", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("Queue(Document? document", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("document?.Database is not Database database", StringComparison.Ordinal) &&
           missingShxRuntime.Contains("TryQueue(e.Document, force: false)", StringComparison.Ordinal),
        "缺失 SHX 文档事件没有隔离空文档，异常可能穿过 AutoCAD 原生事件边界导致崩溃。" );
    Assert(smartInputMethod.Contains("CommandWillStart", StringComparison.Ordinal) &&
           smartInputMethod.Contains("CommandEnded", StringComparison.Ordinal) &&
           smartInputMethod.Contains("CommandCancelled", StringComparison.Ordinal) &&
           smartInputMethod.Contains("CommandFailed", StringComparison.Ordinal) &&
           smartInputMethod.Contains("PromptingForString", StringComparison.Ordinal) &&
           smartInputMethod.Contains("PromptedForString", StringComparison.Ordinal) &&
           smartInputMethod.Contains("ImmSetOpenStatus", StringComparison.Ordinal) &&
           smartInputMethod.Contains("Document? document = e.Document", StringComparison.Ordinal) &&
           smartInputMethod.Contains("document ??= AcadApplication.DocumentManager.MdiActiveDocument", StringComparison.Ordinal) &&
           smartInputMethod.Contains("private static void RunEventSafely(Action action)", StringComparison.Ordinal) &&
           !smartInputMethod.Contains("SendKeys", StringComparison.Ordinal),
        "智能输入法没有覆盖命令和文字输入生命周期，或仍在模拟按键。" );
    Assert(cadQuickToggle.Contains("GetSystemVariable(\"DYNMODE\")", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("SetSystemVariable(\"DYNMODE\"", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("| 1", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("& ~1", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("GetSystemVariable(\"PDMODE\")", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("SetSystemVariable(\"PDMODE\"", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("IsSupportedPointStyle", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("AcadPreferencesApplication.Preferences", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("OpenSave.SaveAsType", StringComparison.Ordinal) &&
           cadQuickToggle.Contains("TryApplyConfiguredSaveFileFormat", StringComparison.Ordinal),
        "CAD 快捷开关没有读取真实 DYNMODE，或未按位保留其他动态输入设置。" );
    Assert(pointStyleIcon.Contains("DrawEllipse", StringComparison.Ordinal) &&
           pointStyleIcon.Contains("DrawRectangle", StringComparison.Ordinal) &&
           pointStyleIcon.Contains("DrawLine", StringComparison.Ordinal),
        "点样式图标没有覆盖圆、方框及十字/叉线绘制。" );
    Assert(guard.Contains("DocumentLockModeChanged", StringComparison.Ordinal) &&
           guard.Contains("e.Veto()", StringComparison.Ordinal) &&
           guard.Contains("模块已禁用", StringComparison.Ordinal),
        "模块关闭后没有在 AutoCAD 命令入口阻止执行并给出提示。" );
    Assert(panelCode.Contains("selectedModule = ToolboxModule.Preferences", StringComparison.Ordinal) &&
           palette.Contains("ToolboxModule? module = null", StringComparison.Ordinal) &&
           palette.Contains("if (module.HasValue)", StringComparison.Ordinal),
        "工具箱首次打开没有默认显示偏好设置，或后续打开不能保留当前模块。" );
}

static void ShxFontSyncCopiesReferencedFontsSafely()
{
    var root = Path.Combine(Path.GetTempPath(), $"dc-shx-sync-{Guid.NewGuid():N}");
    var library = Path.Combine(root, "library");
    var drawingDirectory = Path.Combine(root, "drawing");
    Directory.CreateDirectory(library);
    Directory.CreateDirectory(drawingDirectory);

    try
    {
        File.WriteAllText(Path.Combine(library, "simplex.shx"), "simplex");
        File.WriteAllText(Path.Combine(library, "gbcbig.shx"), "gbcbig");
        File.WriteAllText(Path.Combine(library, "same.shx"), "same");
        File.WriteAllText(Path.Combine(library, "conflict.shx"), "library-version");
        File.WriteAllText(Path.Combine(library, "escape.shx"), "must-not-copy");
        File.WriteAllText(Path.Combine(drawingDirectory, "same.shx"), "same");
        File.WriteAllText(Path.Combine(drawingDirectory, "conflict.shx"), "drawing-version");

        var referencedFonts = new[]
        {
            "simplex.shx",
            "SIMPLEX.SHX",
            @"C:\AutoCAD\Fonts\gbcbig.shx",
            "same.shx",
            "conflict.shx",
            "missing.shx",
            "arial.ttf",
            @"..\escape.shx"
        };

        var first = ShxFontSyncService.Sync(library, drawingDirectory, referencedFonts);
        Assert(first.CopiedCount == 2 && first.ExistingCount == 1 &&
               first.ConflictCount == 1 && first.MissingCount == 1,
            "首次同步没有正确区分复制、已存在、冲突和缺失字体。");
        Assert(File.ReadAllText(Path.Combine(drawingDirectory, "simplex.shx")) == "simplex" &&
               File.ReadAllText(Path.Combine(drawingDirectory, "gbcbig.shx")) == "gbcbig",
            "引用的 SHX 字体没有复制到图纸目录。");
        Assert(File.ReadAllText(Path.Combine(drawingDirectory, "conflict.shx")) == "drawing-version",
            "同名但内容不同的图纸字体被覆盖。");
        Assert(!File.Exists(Path.Combine(drawingDirectory, "escape.shx")) &&
               !File.Exists(Path.Combine(drawingDirectory, "arial.ttf")),
            "路径穿越或非 SHX 文件被错误复制。");

        var second = ShxFontSyncService.Sync(library, drawingDirectory, referencedFonts);
        Assert(second.CopiedCount == 0 && second.ExistingCount == 3 &&
               second.ConflictCount == 1 && second.MissingCount == 1,
            "重复保存时没有跳过内容相同的既有字体。");
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void MissingShxReplacementPolicyIsSafe()
{
    Assert(
        MissingShxReplacementPolicy.Decide(
            "existing.shx",
            ShxReferenceKind.TextFont,
            isAvailable: true,
            isInLibrary: false).Action == MissingShxAction.None,
        "已经可用的 SHX 被错误处理。" );
    Assert(
        MissingShxReplacementPolicy.Decide(
            "custom.shx",
            ShxReferenceKind.TextFont,
            isAvailable: false,
            isInLibrary: true).Action == MissingShxAction.UseLibrary,
        "项目字体库已有的原始 SHX 没有优先保留。" );

    var textDecision = MissingShxReplacementPolicy.Decide(
        "missing-text.shx",
        ShxReferenceKind.TextFont,
        isAvailable: false,
        isInLibrary: false);
    Assert(textDecision.Action == MissingShxAction.Replace &&
           textDecision.ReplacementFile == "simplex.shx",
        "缺失普通文字 SHX 没有替换为 simplex.shx。" );

    var bigFontDecision = MissingShxReplacementPolicy.Decide(
        "missing-big.shx",
        ShxReferenceKind.BigFont,
        isAvailable: false,
        isInLibrary: false);
    Assert(bigFontDecision.Action == MissingShxAction.Replace &&
           bigFontDecision.ReplacementFile == "gbcbig.shx",
        "缺失中文大字体 SHX 没有替换为 gbcbig.shx。" );

    Assert(
        MissingShxReplacementPolicy.Decide(
            "missing-shape.shx",
            ShxReferenceKind.ShapeFile,
            isAvailable: false,
            isInLibrary: false).Action == MissingShxAction.ReportOnly,
        "缺失形文件被错误替换，可能破坏线型或符号。" );
    Assert(
        MissingShxReplacementPolicy.Decide(
            "arial.ttf",
            ShxReferenceKind.TextFont,
            isAvailable: false,
            isInLibrary: false).Action == MissingShxAction.None,
        "非 SHX 字体被错误纳入替换。" );
}

static void GrilleOptionsValidateDefaultsAndDimensions()
{
    var defaults = GrilleOptions.Default;
    Assert(defaults.IsVertical && !defaults.NeedPlanView && !defaults.NeedSideView,
        "格栅板默认方向或辅助视图状态不正确。");
    Assert(defaults.Width == 1000 && defaults.Height == 2400 &&
           defaults.BackThickness == 9 && defaults.StripThickness == 15 &&
           defaults.StripWidth == 30 && defaults.StripGap == 20 &&
           defaults.HatchPattern == "ANSI31",
        "格栅板默认规格与已确认界面不一致。");

    Assert(GrilleOptions.TryCreate(
            false, true, true,
            "1200", "2600", "12", "18", "35", "25", "SOLID",
            out var parsed, out var error),
        $"有效格栅参数未通过校验：{error}");
    Assert(!parsed.IsVertical && parsed.NeedPlanView && parsed.NeedSideView &&
           parsed.Width == 1200 && parsed.Height == 2600 && parsed.HatchPattern == "SOLID",
        "有效格栅参数未被完整读取。");

    Assert(!GrilleOptions.TryCreate(
            true, false, false,
            "1000", "2400", "9", "15", "0", "20", "ANSI31",
            out _, out var stripError) && stripError.Contains("条宽", StringComparison.Ordinal),
        "零条宽未被拒绝。");
    Assert(!GrilleOptions.TryCreate(
            true, false, false,
            "1000", "2400", "9", "15", "30", "-1", "ANSI31",
            out _, out var gapError) && gapError.Contains("条间距", StringComparison.Ordinal),
        "负数条间距未被拒绝。");
}

static void GrilleLayoutSupportsVerticalAndHorizontalDirections()
{
    var vertical = GrilleLayoutCalculator.Calculate(
        10, 20, 180, 130,
        GrilleOptions.Default with { Width = 180, Height = 130 });
    Assert(vertical.Frame == new GrilleRectangle(10, 20, 180, 130),
        "竖向格栅正视图外框不正确。");
    Assert(vertical.FrontStrips.Count == 4 &&
           vertical.FrontStrips.All(rectangle => rectangle.Height == 130 && rectangle.Width == 30),
        "竖向格栅没有按条宽和间距铺排。");
    Assert(vertical.FrontStrips.Select(rectangle => rectangle.X).SequenceEqual([10d, 60d, 110d, 160d]),
        "竖向格栅起点序列不正确。");

    var horizontal = GrilleLayoutCalculator.Calculate(
        10, 20, 180, 130,
        GrilleOptions.Default with { IsVertical = false, Width = 180, Height = 130 });
    Assert(horizontal.FrontStrips.Count == 3 &&
           horizontal.FrontStrips.All(rectangle => rectangle.Width == 180 && rectangle.Height == 30),
        "横向格栅没有按条宽和间距铺排。");
    Assert(horizontal.FrontStrips.Select(rectangle => rectangle.Y).SequenceEqual([20d, 70d, 120d]),
        "横向格栅起点序列不正确。");
}

static void GrilleLayoutBuildsRequestedAuxiliaryViews()
{
    var vertical = GrilleLayoutCalculator.Calculate(
        0, 0, 180, 130,
        GrilleOptions.Default with
        {
            Width = 180,
            Height = 130,
            NeedPlanView = true,
            NeedSideView = true
        });
    Assert(vertical.PlanRectangles.Count == 5,
        "竖向格栅平面图没有包含底板与全部格栅条。");
    Assert(vertical.PlanRectangles[0] == new GrilleRectangle(0, -209, 180, 9),
        "平面图底板位置或厚度不正确。");
    Assert(vertical.SideRectangles.SequenceEqual([
            new GrilleRectangle(380, 0, 9, 130),
            new GrilleRectangle(389, 0, 15, 130)
        ]),
        "竖向格栅侧视图构造不正确。");

    var horizontal = GrilleLayoutCalculator.Calculate(
        0, 0, 180, 130,
        GrilleOptions.Default with
        {
            IsVertical = false,
            Width = 180,
            Height = 130,
            NeedPlanView = true,
            NeedSideView = true
        });
    Assert(horizontal.PlanRectangles.Count == 2,
        "横向格栅平面图应只包含底板与连续前条。");
    Assert(horizontal.SideRectangles.Count == 4,
        "横向格栅侧视图没有包含底板与三条格栅。");
}

static void PerforatedPanelOptionsValidateDefaultsAndArray()
{
    var defaults = PerforatedPanelOptions.Default;
    Assert(!defaults.NeedPlanView && !defaults.NeedSideView &&
           defaults.Width == 600 && defaults.Height == 800 && defaults.Depth == 18 &&
           defaults.InnerOffset == 50 && defaults.HoleDiameter == 10 &&
           defaults.HorizontalCount == 6 && defaults.VerticalCount == 10,
        "洞洞板默认参数与已确认界面不一致。");

    Assert(PerforatedPanelOptions.TryCreate(
            true, true,
            "900", "1200", "20", "60", "12", "8", "12",
            out var parsed, out var error),
        $"有效洞洞板参数未通过校验：{error}");
    Assert(parsed.NeedPlanView && parsed.NeedSideView &&
           parsed.Width == 900 && parsed.Height == 1200 && parsed.Depth == 20 &&
           parsed.InnerOffset == 60 && parsed.HoleDiameter == 12 &&
           parsed.HorizontalCount == 8 && parsed.VerticalCount == 12,
        "有效洞洞板参数未被完整读取。");

    Assert(!PerforatedPanelOptions.TryCreate(
            false, false,
            "600", "800", "18", "50", "10", "0", "10",
            out _, out var countError) && countError.Contains("横向孔数", StringComparison.Ordinal),
        "零横向孔数未被拒绝。");
    Assert(!PerforatedPanelOptions.TryCreate(
            false, false,
            "600", "800", "18", "4", "10", "6", "10",
            out _, out var offsetError) && offsetError.Contains("内偏移", StringComparison.Ordinal),
        "不能容纳孔半径的内偏移未被拒绝。");
}

static void PerforatedPanelLayoutUsesInnerOffsets()
{
    var layout = PerforatedPanelLayoutCalculator.Calculate(
        10, 20, 600, 800, PerforatedPanelOptions.Default);

    Assert(layout.Frame == new PerforatedPanelRectangle(10, 20, 600, 800),
        "洞洞板正视图外框不正确。");
    Assert(layout.Holes.Count == 60, "洞洞板孔数量不正确。");
    Assert(layout.Holes[0] == new PerforatedHole(60, 70, 5),
        "洞洞板左下孔没有使用四边内偏移定位。");
    Assert(layout.Holes[^1] == new PerforatedHole(560, 770, 5),
        "洞洞板右上孔没有使用四边内偏移定位。");
    Assert(layout.Holes.Select(hole => hole.X).Distinct().Count() == 6 &&
           layout.Holes.Select(hole => hole.Y).Distinct().Count() == 10,
        "洞洞板孔阵列行列数不正确。");
}

static void PerforatedPanelLayoutBuildsRequestedAuxiliaryViews()
{
    var layout = PerforatedPanelLayoutCalculator.Calculate(
        10, 20, 600, 800,
        PerforatedPanelOptions.Default with { NeedPlanView = true, NeedSideView = true });

    Assert(layout.PlanRectangles.SequenceEqual([
            new PerforatedPanelRectangle(10, -198, 600, 18)
        ]),
        "洞洞板俯视图位置或厚度不正确。");
    Assert(layout.SideRectangles.SequenceEqual([
            new PerforatedPanelRectangle(810, 20, 18, 800)
        ]),
        "洞洞板侧视图位置或厚度不正确。");
}

static void PerforatedPanelLayoutBuildsRepresentativeDimensions()
{
    var layout = PerforatedPanelLayoutCalculator.Calculate(
        10, 20, 600, 800, PerforatedPanelOptions.Default);

    Assert(layout.LinearDimensions.Count == 4,
        "洞洞板不应为全部孔距重复生成标尺。");
    Assert(layout.LinearDimensions.SequenceEqual([
            new PerforatedLinearDimension(10, 20, 60, 20, 35, -10),
            new PerforatedLinearDimension(60, 20, 160, 20, 110, -10),
            new PerforatedLinearDimension(10, 20, 10, 70, -20, 45),
            new PerforatedLinearDimension(10, 70, 10, 147.77777777777777, -20, 108.88888888888889)
        ]),
        "洞洞板代表性距边或孔距标尺位置不正确。");
    Assert(layout.DiameterDimension == new PerforatedDiameterDimension(60, 70, 5),
        "洞洞板孔径标尺没有关联到代表孔。");

    var single = PerforatedPanelLayoutCalculator.Calculate(
        0, 0, 600, 800,
        PerforatedPanelOptions.Default with { HorizontalCount = 1, VerticalCount = 1 });
    Assert(single.LinearDimensions.Count == 2,
        "单行单列洞洞板不应生成不存在的孔距标尺。");
}

static void PerforatedDimensionsUseRequestedAngleAndArrowScale()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "WainscotCommands.cs");
    var commands = File.ReadAllText(commandsPath);

    Assert(commands.Contains("var diameterDirection = Math.Sqrt(0.5);", StringComparison.Ordinal) &&
           commands.Contains(
               "diameter.CenterX + diameter.Radius * diameterDirection,\n                        diameter.CenterY + diameter.Radius * diameterDirection",
               StringComparison.Ordinal),
        "洞洞板直径标尺没有镜像到右上方45度。");
    Assert(commands.Contains("var dimensionArrowSize = dimensionStyle.Dimasz / 6d;", StringComparison.Ordinal) &&
           commands.Split("Dimasz = dimensionArrowSize", StringSplitOptions.None).Length == 3,
        "洞洞板线性标尺和直径标尺没有统一使用六分之一箭头大小。");
}

static void PerforatedDimensionsUseRequestedTextAndExtensionScale()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "WainscotCommands.cs");
    var commands = File.ReadAllText(commandsPath);

    Assert(commands.Contains("var dimensionTextHeight = dimensionStyle.Dimtxt * 0.5;", StringComparison.Ordinal),
        "洞洞板标尺文字没有在当前基础上放大一倍。");
    Assert(commands.Contains("var dimensionExtensionBeyondLine = dimensionStyle.Dimexe / 5d;", StringComparison.Ordinal) &&
           commands.Split("Dimexe = dimensionExtensionBeyondLine", StringSplitOptions.None).Length == 3,
        "洞洞板标尺超出尺寸线没有统一缩小为当前样式的五分之一。");
    Assert(commands.Contains("var dimensionOriginOffset = Math.Max(dimensionStyle.Dimexo, dimensionArrowSize);", StringComparison.Ordinal) &&
           commands.Split("Dimexo = dimensionOriginOffset", StringSplitOptions.None).Length == 3,
        "洞洞板标尺没有显示起点偏移量。");
}

static void WainscotGrilleSurfaceContractIsComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panelCodePath = Path.ChangeExtension(panelPath, ".xaml.cs");
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "WainscotCommands.cs");
    var applicationPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs");
    var palettePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ToolboxPalette.cs");
    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelCodePath);
    var commands = File.ReadAllText(commandsPath);
    var application = File.ReadAllText(applicationPath);
    var palette = File.ReadAllText(palettePath);

    Assert(panel.Contains("x:Name=\"WallPanelModuleButton\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"墙板\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"WallPanelModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"格栅板\"", StringComparison.Ordinal),
        "墙板左侧入口或格栅板页缺失。");
    Assert(panel.Contains("x:Name=\"GrilleWidthBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"GrilleHatchCombo\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"生成格栅板 (DCGS)\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"内空填充格栅 (DCGST)\"", StringComparison.Ordinal),
        "格栅参数或DCGS/DCGST操作入口不完整。");
    Assert(panelCode.Contains("GrilleOptions.TryCreate", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_WAINSCOT_GRILLE_DRAW", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_WAINSCOT_GRILLE_FILL", StringComparison.Ordinal),
        "格栅面板未校验参数并调用稳定底层命令。");
    Assert(commands.Contains("[CommandMethod(\"DC_CMD_WAINSCOT_GRILLE_DRAW\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"墙板\", \"生成格栅板\", \"DCGS\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DC_CMD_WAINSCOT_GRILLE_FILL\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"墙板\", \"内空填充格栅\", \"DCGST\")]", StringComparison.Ordinal) &&
           commands.Contains("DrawingLayerRuntimeSettings.WallHatchScale", StringComparison.Ordinal) &&
           commands.Contains("TraceBoundary", StringComparison.Ordinal) &&
           commands.Contains("GeometricExtents", StringComparison.Ordinal),
        "DCGS/DCGST底层命令、快捷键登记、填充比例或闭合内空识别不完整。");
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.WainscotCommands))", StringComparison.Ordinal),
        "墙板命令类未注册到插件入口。");
    Assert(palette.Contains("WallPanels", StringComparison.Ordinal) &&
           panelCode.Contains("ToolboxModule.WallPanels", StringComparison.Ordinal),
        "墙板模块未接入工具箱导航。");
}

static void WainscotPerforatedSurfaceContractIsComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panelCodePath = Path.ChangeExtension(panelPath, ".xaml.cs");
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "WainscotCommands.cs");
    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelCodePath);
    var commands = File.ReadAllText(commandsPath);

    Assert(panel.Contains("Header=\"洞洞板\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"PerforatedWidthBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"PerforatedHorizontalCountBox\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"生成洞洞板 (DCDB)\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"内空填充洞洞板 (DCDBT)\"", StringComparison.Ordinal),
        "洞洞板页、参数或DCDB/DCDBT操作入口不完整。");
    Assert(panelCode.Contains("PerforatedPanelOptions.TryCreate", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_WAINSCOT_PERFORATED_DRAW", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_WAINSCOT_PERFORATED_FILL", StringComparison.Ordinal),
        "洞洞板面板未校验参数并调用稳定底层命令。");
    Assert(commands.Contains("[CommandMethod(\"DC_CMD_WAINSCOT_PERFORATED_DRAW\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"墙板\", \"生成洞洞板\", \"DCDB\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DC_CMD_WAINSCOT_PERFORATED_FILL\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"墙板\", \"内空填充洞洞板\", \"DCDBT\")]", StringComparison.Ordinal) &&
           commands.Contains("AppendCircle", StringComparison.Ordinal) &&
           commands.Contains("new AlignedDimension", StringComparison.Ordinal) &&
           commands.Contains("new DiametricDimension", StringComparison.Ordinal) &&
           commands.Contains("dimensionStyle.Dimtxt * 0.5", StringComparison.Ordinal),
        "DCDB/DCDBT底层命令、快捷键登记、圆孔或半高标尺生成不完整。");
}

static void BackgroundWallOptionsValidateDefaultsAndRows()
{
    var defaults = BackgroundWallOptions.Default;
    Assert(defaults.NeedPlanView && defaults.NeedSideView && defaults.UseRandomWidths &&
           !defaults.LeaveVerticalGap && !defaults.GenerateAllPlanRows &&
           defaults.TotalWidth == 2400 && defaults.Depth == 18 && defaults.Gap == 3 &&
           defaults.CreateSlots && defaults.Rows.Count == 1 &&
           defaults.Rows[0] == new BackgroundWallRowOptions(4, 700),
        "背景墙默认参数与已确认界面不一致。");

    Assert(BackgroundWallOptions.TryCreate(
            true, false, false, true, true, "2400", "18", "3", true, "2",
            [new BackgroundWallRowInput("3", "600"), new BackgroundWallRowInput("4", "700")],
            out var parsed,
            out var error),
        $"有效背景墙参数未通过校验：{error}");
    Assert(!parsed.UseRandomWidths && parsed.LeaveVerticalGap && parsed.GenerateAllPlanRows &&
           parsed.Rows.SequenceEqual([
            new BackgroundWallRowOptions(3, 600),
            new BackgroundWallRowOptions(4, 700)
        ]), "背景墙分排参数未完整读取。");

    Assert(!BackgroundWallOptions.TryCreate(
            false, false, true, false, false, "250", "18", "3", false, "1",
            [new BackgroundWallRowInput("3", "700")],
            out _, out var widthError) && widthError.Contains("最小板宽", StringComparison.Ordinal),
        "随机背景墙未拒绝无法满足最小板宽的规格。");
    Assert(!BackgroundWallOptions.TryCreate(
            false, false, false, false, false, "2400", "18", "3", false, "0",
            [], out _, out var rowError) && rowError.Contains("横向排数", StringComparison.Ordinal),
        "零排数未被拒绝。");
}

static void BackgroundWallLayoutStacksRowsFromBottom()
{
    var options = new BackgroundWallOptions(
        true,
        false,
        false,
        false,
        false,
        1000,
        18,
        20,
        false,
        [new BackgroundWallRowOptions(2, 300), new BackgroundWallRowOptions(3, 400)]);
    var layout = BackgroundWallLayoutCalculator.Calculate(10, 20, options);

    Assert(layout.Frame == new BackgroundWallRectangle(10, 20, 1000, 700),
        "背景墙默认总外框不应保留垂直排缝。");
    Assert(layout.FrontPanels.Count == 5 &&
           layout.FrontPanels.Take(2).All(panel => panel.RowIndex == 0 && panel.Y == 20 && panel.Height == 300) &&
           layout.FrontPanels.Skip(2).All(panel => panel.RowIndex == 1 && panel.Y == 320 && panel.Height == 400),
        "背景墙分排没有从底部无缝堆叠。");
    Assert(layout.FrontPanels.Take(2).Select(panel => panel.Width).SequenceEqual([490d, 490d]) &&
           layout.FrontPanels.Skip(2).Select(panel => panel.Width).SequenceEqual([320d, 320d, 320d]),
        "背景墙等分模式没有扣除板缝后均分宽度。");
    Assert(layout.PlanRectangles.Count == 2 &&
           layout.PlanRectangles.Select(rectangle => rectangle.X).SequenceEqual(layout.FrontPanels.Take(2).Select(panel => panel.X)) &&
           layout.PlanRectangles.Select(rectangle => rectangle.Width).SequenceEqual(layout.FrontPanels.Take(2).Select(panel => panel.Width)) &&
           layout.PlanRectangles.All(rectangle => rectangle.Y == -198 && rectangle.Height == 18),
        "背景墙默认俯视图没有只生成最底排。 ");
}

static void BackgroundWallLayoutSupportsVerticalGapsAndAllPlanRows()
{
    var options = new BackgroundWallOptions(
        true,
        false,
        false,
        true,
        true,
        1000,
        18,
        20,
        false,
        [new BackgroundWallRowOptions(2, 300), new BackgroundWallRowOptions(3, 400)]);
    var layout = BackgroundWallLayoutCalculator.Calculate(10, 20, options);

    Assert(layout.Frame == new BackgroundWallRectangle(10, 20, 1000, 720) &&
           layout.FrontPanels.Skip(2).All(panel => panel.Y == 340),
        "启用垂直留缝后没有按缝隙值分隔上下排。 ");
    Assert(layout.PlanRectangles.Count == layout.FrontPanels.Count &&
           layout.PlanRectangles.Select(rectangle => rectangle.X).SequenceEqual(layout.FrontPanels.Select(panel => panel.X)) &&
           layout.PlanRectangles.Select(rectangle => rectangle.Width).SequenceEqual(layout.FrontPanels.Select(panel => panel.Width)) &&
           layout.PlanRectangles.Take(2).All(rectangle => rectangle.Y == -198 && rectangle.Height == 18) &&
           layout.PlanRectangles.Skip(2).All(rectangle => rectangle.Y == -256 && rectangle.Height == 18),
        "启用全部排俯视图后没有逐排对应立面板宽、板缝和排次。 ");
}

static void BackgroundWallLayoutBuildsStableRandomSlotsAndViews()
{
    var options = new BackgroundWallOptions(
        true,
        true,
        true,
        false,
        false,
        1000,
        18,
        10,
        true,
        [new BackgroundWallRowOptions(4, 700)]);
    var first = BackgroundWallLayoutCalculator.Calculate(0, 0, options);
    var second = BackgroundWallLayoutCalculator.Calculate(0, 0, options);

    Assert(first.FrontPanels.SequenceEqual(second.FrontPanels) &&
           first.FrontPanels.All(panel => panel.Width >= BackgroundWallOptions.MinimumPanelWidth) &&
           Math.Abs(first.FrontPanels.Sum(panel => panel.Width) + 30 - 1000) < 0.000001,
        "背景墙随机宽度没有保持稳定、最小板宽或总宽度守恒。");
    Assert(first.PlanRectangles.Count == first.FrontPanels.Count &&
           first.PlanRectangles.Zip(first.FrontPanels).All(pair =>
               pair.First.X == pair.Second.X && pair.First.Width == pair.Second.Width) &&
           first.PlanRectangles.All(rectangle => rectangle.Y == -218 && rectangle.Height == 18),
        "背景墙俯视大矩形没有保持立面板的宽度和板缝。");
    Assert(first.PlanSlots.Count == first.FrontPanels.Count - 1 &&
           first.PlanSlots.All(slot => slot.Width == 25 && slot.Height == 5) &&
           first.PlanSlots.Select((slot, index) => new
           {
               Slot = slot,
               LeftPanel = first.FrontPanels[index],
               RightPanel = first.FrontPanels[index + 1],
               LeftPlan = first.PlanRectangles[index],
               RightPlan = first.PlanRectangles[index + 1]
           }).All(item =>
                item.Slot.X + item.Slot.Width / 2 == item.LeftPanel.X + item.LeftPanel.Width + options.Gap / 2 &&
                item.LeftPlan.X + item.LeftPlan.Width + options.Gap == item.RightPlan.X),
        "背景墙卡槽没有对准板缝中心，或俯视大板没有保持立面图板缝。");
    Assert(first.SideRectangles.SequenceEqual([new BackgroundWallRectangle(1200, 0, 18, 700)]),
        "背景墙侧视图位置不正确。");
}

static void WainscotBackgroundWallSurfaceContractIsComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panelCodePath = Path.ChangeExtension(panelPath, ".xaml.cs");
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "WainscotCommands.cs");
    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelCodePath);
    var commands = File.ReadAllText(commandsPath);

    Assert(panel.Contains("Header=\"背景墙\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BackgroundWallRowCountCombo\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BackgroundWallSlotCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BackgroundWallVerticalGapCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BackgroundWallAllPlanRowsCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BackgroundWallRow1Label\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"列数\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"高度\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"生成背景墙 (DCBJQ)\"", StringComparison.Ordinal),
        "背景墙页、分排参数、卡槽或生成入口不完整。");
    Assert(panelCode.Contains("BackgroundWallOptions.TryCreate", StringComparison.Ordinal) &&
           panelCode.Contains("GetBackgroundWallRowLabel", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_WAINSCOT_BACKGROUND_DRAW", StringComparison.Ordinal),
        "背景墙面板未校验参数并调用稳定底层命令。");
    Assert(commands.Contains("[CommandMethod(\"DC_CMD_WAINSCOT_BACKGROUND_DRAW\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"墙板\", \"生成背景墙\", \"DCBJQ\")]", StringComparison.Ordinal) &&
           commands.Contains("AppendBackgroundWall", StringComparison.Ordinal),
        "DCBJQ底层命令、快捷键登记或背景墙落图不完整。");
}

static void SmartAnnotationSurfaceAndSafeStyleSyncContractIsComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var xaml = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "SmartAnnotationCommands.cs");
    var syncPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DimensionStyleSyncService.cs");
    var bridgePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "LeishenDimensionConfigBridge.cs");

    Assert(File.Exists(commandsPath) && File.Exists(syncPath) && File.Exists(bridgePath),
        "智能标注命令或安全样式同步服务尚未建立。");
    var commands = File.ReadAllText(commandsPath);
    var sync = File.ReadAllText(syncPath);
    var bridge = File.ReadAllText(bridgePath);
    Assert(xaml.Contains("x:Name=\"SmartAnnotationModuleButton\"", StringComparison.Ordinal) &&
           xaml.Contains("Text=\"智能标注\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"SmartAnnotationModuleView\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"柜体标注 (DCBC)\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"门板标注 (DCBM)\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"单边标注 (DCBD)\"", StringComparison.Ordinal) &&
           xaml.Contains("Content=\"清除所有标注 (DCBQ)\"", StringComparison.Ordinal),
        "智能标注导航、参数区或主要命令按钮不完整。");
    Assert(panel.Contains("LoadSmartAnnotationModule();", StringComparison.Ordinal) &&
           panel.Contains("SmartAnnotationRuntimeSettings.Save", StringComparison.Ordinal) &&
           panel.Contains("DC_CMD_DIM_CABINET", StringComparison.Ordinal) &&
           panel.Contains("DC_CMD_DIM_DELETE_ALL", StringComparison.Ordinal),
        "智能标注面板没有读取保存参数或路由底层命令。");
    Assert(commands.Contains("LS_CMD_DIM_CABINET_ALL", StringComparison.Ordinal) &&
           commands.Contains("LS_CMD_DIM_SMART_SINGLE", StringComparison.Ordinal) &&
           commands.Contains("LS_CMD_DIM_DELETE_ONLY", StringComparison.Ordinal) &&
           commands.Contains("DimensionStyleSyncService.TrySynchronize", StringComparison.Ordinal),
        "智能标注没有复用既有雷神命令或没有在执行前统一同步样式。");
    Assert(sync.Contains("targetDatabase.DimStyleTableId", StringComparison.Ordinal) &&
           sync.Contains("sourceDatabase.WblockCloneObjects", StringComparison.Ordinal) &&
           sync.Contains("DuplicateRecordCloning.Replace", StringComparison.Ordinal) &&
           !sync.Contains("targetDatabase.CurrentSpaceId", StringComparison.Ordinal),
        "标注样式没有克隆到目标标注样式表，仍可能触发 eInvalidInput。");
    Assert(bridge.Contains("root[\"UseTemplateStyle\"] = false", StringComparison.Ordinal) &&
           bridge.Contains("IsIncludeGenericGraphics", StringComparison.Ordinal) &&
           bridge.Contains("Compatibility.MoveFileReplacingExisting", StringComparison.Ordinal),
        "外部标注命令仍可能重复同步模板，或参数桥接没有原子保存。");
}

static void HardwareLibraryCreatesFoldersAndPairsPreviews()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcHardware-{Guid.NewGuid():N}");

    try
    {
        var defaultFolders = HardwareLibraryService.EnsureDefaultFolders(root);
        Assert(defaultFolders.RootDirectory == Path.GetFullPath(Path.Combine(root, "Modules")),
            "默认素材根目录没有定位到插件目录下的Modules。");

        var folders = HardwareLibraryService.EnsureFolders(root);
        Assert(Directory.Exists(folders.HangingRodDirectory), "未创建挂衣杆素材目录。");
        Assert(Directory.Exists(folders.LightStripDirectory), "未创建灯带素材目录。");
        Assert(Directory.Exists(folders.HandleDirectory), "未创建抽面拉手素材目录。");
        Assert(File.Exists(folders.InstructionsPath), "未创建五金素材说明文件。");

        File.WriteAllBytes(Path.Combine(folders.HangingRodDirectory, "01挂衣杆.dwg"), [1]);
        File.WriteAllBytes(Path.Combine(folders.HangingRodDirectory, "01挂衣杆.png"), [2]);
        File.WriteAllBytes(Path.Combine(folders.HangingRodDirectory, "02挂衣杆.dwg"), [3]);
        File.WriteAllText(Path.Combine(folders.HangingRodDirectory, "忽略.txt"), "ignore");

        var items = HardwareLibraryService.Scan(folders.HangingRodDirectory);
        Assert(items.Count == 2, "五金素材扫描没有只返回DWG文件。");
        Assert(items[0].Name == "01挂衣杆" &&
               items[0].PreviewPath?.EndsWith("01挂衣杆.png", StringComparison.OrdinalIgnoreCase) == true,
            "没有按同名规则匹配挂衣杆预览图。");
        Assert(items[1].PreviewPath is null, "无同名图片的素材被错误匹配了预览。");

        File.WriteAllBytes(Path.Combine(folders.HandleDirectory, "牛角拉手（160）.dwg"), [4]);
        File.WriteAllBytes(Path.Combine(folders.HandleDirectory, "牛角拉手（160）.png"), [5]);
        File.WriteAllBytes(Path.Combine(folders.HandleDirectory, "牛角拉手（160）.bak"), [6]);
        var handles = HardwareLibraryService.Scan(folders.HandleDirectory);
        Assert(handles.Count == 1 &&
               handles[0].Name == "牛角拉手（160）" &&
               handles[0].PreviewPath?.EndsWith("牛角拉手（160）.png", StringComparison.OrdinalIgnoreCase) == true,
            "抽面拉手库没有读取中文动态块 DWG、匹配同名预览或忽略 BAK。" );
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void DoorPanelOptionsUseInnerOpeningAndCoverExtensions()
{
    var defaults = DoorPanelOptions.Default;
    Assert(defaults.DoorCount == 2 && defaults.Gap == 3,
        "立面门板没有默认使用双开门和3mm门缝。");
    Assert(defaults.TopExtension == 0 && defaults.BottomExtension == 0 &&
           defaults.LeftExtension == 0 && defaults.RightExtension == 0,
        "四向盖板延伸值默认值不是0。" );
    Assert(defaults.GenerateOpeningLines && defaults.GenerateHinges && defaults.AvoidShelves,
        "开门线、铰链或层板避让没有默认启用。" );

    Assert(DoorPanelOptions.TryCreate(
            DoorPanelCalculationMode.DoorRoundedGapDistributed,
            "2", "3", "18", "12", "16", "16",
            true, true, true,
            out var options,
            out var error),
        $"合法门板参数未通过验证：{error}");
    Assert(options.TopExtension == 18 && options.BottomExtension == 12 &&
           options.LeftExtension == 16 && options.RightExtension == 16,
        "四向盖板延伸值没有独立保存。" );
    Assert(!DoorPanelOptions.TryCreate(
            DoorPanelCalculationMode.DoorRoundedGapDistributed,
            "2", "3", "-1", "0", "0", "0",
            true, true, true,
            out _,
            out _),
        "负数盖板延伸值未被拒绝。" );
}

static void DoorPanelLayoutAppliesCoverExtensions()
{
    Assert(DoorPanelOptions.TryCreate(
            DoorPanelCalculationMode.GapRoundedDoorDistributed,
            "2", "3", "18", "12", "16", "20",
            true, true, true,
            out var options,
            out var error),
        $"合法门板参数未通过验证：{error}");

    var layout = DoorPanelLayoutCalculator.Calculate(100, 900, 200, 2200, options);
    Assert(layout.Bounds.Left == 84 && layout.Bounds.Right == 920 &&
           layout.Bounds.Bottom == 188 && layout.Bounds.Top == 2218,
        "四向盖板延伸值没有进入最终门板外边界。" );
    Assert(layout.Panels.Count == 2,
        "默认双开门没有生成两块门板。" );
    Assert(Math.Abs(layout.Panels[0].Left - layout.Bounds.Left) < 0.000001 &&
           Math.Abs(layout.Panels[^1].Right - layout.Bounds.Right) < 0.000001,
        "门板没有完整覆盖应用盖板值后的最终边界。" );
    Assert(Math.Abs(layout.Panels[1].Left - layout.Panels[0].Right - (3d + 1d / 3d)) < 0.000001,
        "门板间缝没有按门扇数补偿公式生成。" );
}

static void DoorPanelLayoutsUseUnifiedGapCompensation()
{
    foreach (var doorCount in new[] { 2, 3, 10, 100 })
    {
        var expectedGap = 3d + (doorCount - 1d) / (doorCount + 1d);
        var elevation = DoorPanelLayoutCalculator.Calculate(
            0,
            10000,
            0,
            2400,
            DoorPanelOptions.Default with
            {
                CalculationMode = DoorPanelCalculationMode.GapFixedDoorDistributed,
                DoorCount = doorCount,
                Gap = 3
            });
        var elevationGap = elevation.Panels[1].Left - elevation.Panels[0].Right;
        var plan = PlanDoorPanelLayoutCalculator.Calculate(
            new PlanDoorPanelPoint(0, 0),
            new PlanDoorPanelPoint(10000, 0),
            new PlanDoorPanelPoint(0, -100),
            PlanDoorPanelOptions.Default with
            {
                CalculationMode = DoorPanelCalculationMode.GapFixedDoorDistributed,
                DoorCount = doorCount,
                Gap = 3
            },
            flipDirection: false);

        Assert(Math.Abs(elevationGap - expectedGap) < 0.000001,
            $"{doorCount} 扇立面门板实际门缝未按补偿公式生成：{elevationGap}。" );
        Assert(Math.Abs(plan.ActualGap - expectedGap) < 0.000001,
            $"{doorCount} 扇俯视门板实际门缝未按补偿公式生成：{plan.ActualGap}。" );
    }

    var roundedElevation = DoorPanelLayoutCalculator.Calculate(
        0,
        1000,
        0,
        2400,
        DoorPanelOptions.Default with { DoorCount = 3, Gap = 3 });
    Assert(Math.Abs(roundedElevation.Panels[1].Left - roundedElevation.Panels[0].Right - 3.5) < 0.000001 &&
           roundedElevation.Panels[^1].Right == roundedElevation.Bounds.Right,
        "门取整模式没有保持统一补偿门缝，或末扇没有吸收取整余量。" );

    var roundedGapElevation = DoorPanelLayoutCalculator.Calculate(
        0,
        1000,
        0,
        2400,
        DoorPanelOptions.Default with
        {
            CalculationMode = DoorPanelCalculationMode.DoorAndGapRoundedRemainderRight,
            DoorCount = 3,
            Gap = 3.2
        });
    Assert(Math.Abs(roundedGapElevation.Panels[1].Left - roundedGapElevation.Panels[0].Right - 3.5) < 0.000001 &&
           roundedGapElevation.Panels[^1].Right == roundedGapElevation.Bounds.Right,
        "门缝取整模式没有先取整设定缝再叠加补偿，或末扇没有吸收余量。" );

    var compactElevation = DoorPanelLayoutCalculator.Calculate(
        0,
        4,
        0,
        10,
        DoorPanelOptions.Default with { DoorCount = 2, Gap = 0 });
    Assert(compactElevation.Panels.Count == 2 &&
           compactElevation.Panels.All(panel => panel.Width > 0) &&
           compactElevation.Panels[^1].Right == compactElevation.Bounds.Right,
        "补偿算法被旧分格算法的边界校验误拦截。" );
}

static void DoorOpeningDetectorFindsNearestIndependentLineBay()
{
    var segments = new[]
    {
        new DoorOpeningSegment(0, 0, 0, 2200),
        new DoorOpeningSegment(600, 0, 600, 2200),
        new DoorOpeningSegment(1200, 0, 1200, 2200),
        new DoorOpeningSegment(0, 0, 1200, 0),
        new DoorOpeningSegment(0, 700, 1200, 700),
        new DoorOpeningSegment(0, 1500, 1200, 1500),
        new DoorOpeningSegment(0, 2200, 1200, 2200),
        new DoorOpeningSegment(200, 900, 500, 900)
    };

    Assert(DoorOpeningDetector.TryDetect(900, 1000, segments, out var bounds),
        "由独立横竖线组成的柜格没有被识别。" );
    Assert(bounds == new DoorPanelBounds(600, 1200, 700, 1500),
        $"识别到了错误柜格：{bounds}。" );
}

static void DoorOpeningDetectorRejectsIncompleteOrInvalidBounds()
{
    var incomplete = new[]
    {
        new DoorOpeningSegment(0, 0, 0, 1000),
        new DoorOpeningSegment(600, 0, 600, 1000),
        new DoorOpeningSegment(0, 0, 600, 0)
    };
    Assert(!DoorOpeningDetector.TryDetect(300, 500, incomplete, out _),
        "缺少上边界的柜格不应进入门板预览。" );

    var unrelated = new[]
    {
        new DoorOpeningSegment(0, 0, 0, 1000),
        new DoorOpeningSegment(600, 0, 600, 1000),
        new DoorOpeningSegment(100, 0, 500, 0),
        new DoorOpeningSegment(100, 1000, 500, 1000)
    };
    Assert(!DoorOpeningDetector.TryDetect(300, 500, unrelated, out _),
        "没有跨越左右边界的短横线不应被当作柜格上下边界。" );
}

static void DoorPanelSingleDoorHingeSideCanBeSwitched()
{
    var options = DoorPanelOptions.Default with { DoorCount = 1 };
    var left = DoorPanelLayoutCalculator.Calculate(0, 600, 0, 900, options, singleDoorHingeOnLeft: true);
    var right = DoorPanelLayoutCalculator.Calculate(0, 600, 0, 900, options, singleDoorHingeOnLeft: false);

    Assert(left.Panels.Single().HingeOnLeft && !right.Panels.Single().HingeOnLeft,
        "单门铰链方向没有随 L/R 预览状态切换。" );
}

static void DoorPanelPreviewUsesCadNativeWakeUp()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelCommands.cs");
    var commands = File.ReadAllText(commandsPath);

    Assert(commands.Contains("AcadApplication.PreTranslateMessage += OnPreTranslateMessage", StringComparison.Ordinal) &&
           commands.Contains("AcadApplication.PreTranslateMessage -= OnPreTranslateMessage", StringComparison.Ordinal) &&
           commands.Contains("WakeUpJig();", StringComparison.Ordinal),
        "门板数量热键没有通过 AutoCAD 原生消息入口唤醒 Jig。" );
    Assert(!commands.Contains("ComponentDispatcher.ThreadPreprocessMessage", StringComparison.Ordinal) &&
           !commands.Contains("Dispatcher.CurrentDispatcher.BeginInvoke", StringComparison.Ordinal) &&
           !commands.Contains("_editor.UpdateScreen()", StringComparison.Ordinal),
        "门板预览仍混用 WPF Dispatcher 与 CAD Drag 消息循环。" );
}

static void DoorPanelPreviewDefersOpeningLinesAndHingesUntilConfirmation()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelCommands.cs");
    var commands = File.ReadAllText(commandsPath);
    var previewStart = commands.IndexOf("protected override bool WorldDraw", StringComparison.Ordinal);
    var previewEnd = commands.IndexOf("private bool CanCalculate", previewStart, StringComparison.Ordinal);
    var preview = commands[previewStart..previewEnd];

    Assert(preview.Contains("CreatePreviewOutline", StringComparison.Ordinal) &&
           preview.Contains("CreatePreviewDividerLines", StringComparison.Ordinal),
        "门板预览没有收敛为总外框和分隔线。" );
    Assert(!preview.Contains("CreateOpeningLines", StringComparison.Ordinal) &&
           !preview.Contains("WritePanelHinges", StringComparison.Ordinal),
        "门板预览阶段仍在生成开门线或铰链。" );

    var writeStart = commands.IndexOf("private static void WriteLayout", StringComparison.Ordinal);
    var writeEnd = commands.IndexOf("private static void WritePanelHinges", writeStart, StringComparison.Ordinal);
    var writeLayout = commands[writeStart..writeEnd];
    Assert(writeLayout.Contains("CreateOpeningLines", StringComparison.Ordinal) &&
           writeLayout.Contains("WritePanelHinges", StringComparison.Ordinal),
        "左键确认后的正式生成流程缺少开门线或铰链。" );
}

static void DoorPanelDefaultFlowRequiresTwoCorners()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelCommands.cs");
    var commands = File.ReadAllText(commandsPath);
    var entryStart = commands.IndexOf("public static void GenerateElevationDoor", StringComparison.Ordinal);
    var entryEnd = commands.IndexOf("private static bool TryPromptTwoPointBounds", entryStart, StringComparison.Ordinal);
    var entry = commands[entryStart..entryEnd];

    Assert(entry.Contains("TryPromptTwoPointBounds(editor, out var innerBounds)", StringComparison.Ordinal),
        "门板命令第一次点击仍会直接执行内空识别，没有默认等待第二个对角点。" );
    Assert(!entry.Contains("TryPromptOpeningBounds", StringComparison.Ordinal),
        "门板命令默认入口仍保留一次点击自动识别路径。" );
}

static void DoorPanelSurfaceContractIsComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panelCodePath = panelPath + ".cs";
    var palettePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ToolboxPalette.cs");
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelCommands.cs");
    var applicationPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs");

    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelCodePath);
    var palette = File.ReadAllText(palettePath);
    var commands = File.ReadAllText(commandsPath);
    var application = File.ReadAllText(applicationPath);
    Assert(panel.Contains("Text=\"门板抽面\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DoorPanelModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"立面门板\"", StringComparison.Ordinal),
        "左侧门板抽面入口或立面门板页签缺失。" );
    foreach (var controlName in new[]
             {
                 "DoorPanelCalculationModeCombo",
                 "DoorPanelTopExtensionBox", "DoorPanelBottomExtensionBox",
                 "DoorPanelLeftExtensionBox", "DoorPanelRightExtensionBox",
                 "DoorPanelHandlelessProfileModeCombo", "DoorPanelHandlelessGapBox",
                 "DoorPanelHandlelessProfileSizeBox"
             })
    {
        Assert(panel.Contains($"x:Name=\"{controlName}\"", StringComparison.Ordinal),
            $"立面门板缺少参数控件：{controlName}。" );
    }

    Assert(panel.Contains("生成开门线（默认虚线）", StringComparison.Ordinal) &&
           panel.Contains("自动生成铰链", StringComparison.Ordinal) &&
           panel.Contains("自动识别层板并避让铰链", StringComparison.Ordinal),
        "立面门板缺少开门线、统一铰链参数或层板避让选项。" );
    var doorPanelStart = panel.IndexOf("x:Name=\"DoorPanelModuleView\"", StringComparison.Ordinal);
    var hardwareStart = panel.IndexOf("x:Name=\"HardwareModuleView\"", StringComparison.Ordinal);
    var hingeSettings = panel.IndexOf("Header=\"铰链设置\"", StringComparison.Ordinal);
    Assert(doorPanelStart >= 0 && hingeSettings > doorPanelStart && hingeSettings < hardwareStart,
        "铰链设置没有移动到门板抽面模块。" );
    Assert(panelCode.Contains("DoorPanelOptions.TryCreate", StringComparison.Ordinal) &&
           panelCode.Contains("DoorPanelRuntimeSettings.Current", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_ELEVATION_DOOR", StringComparison.Ordinal) &&
           panelCode.Contains("TryReadDoorHandlelessProfileOptions", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_DRAWER_HANDLELESS_PROFILE", StringComparison.Ordinal) &&
           panel.Contains("Click=\"OnGenerateDoorHandlelessProfileClick\"", StringComparison.Ordinal),
        "门板参数尚未接入验证、运行时设置和绘图命令。" );
    Assert(commands.Contains("DoorPanelLayoutCalculator.Calculate", StringComparison.Ordinal) &&
           commands.Contains("TopExtension", StringComparison.Ordinal) &&
           commands.Contains("BottomExtension", StringComparison.Ordinal) &&
           commands.Contains("LeftExtension", StringComparison.Ordinal) &&
           commands.Contains("RightExtension", StringComparison.Ordinal),
        "门板绘图命令没有消费四向盖板延伸值。" );
    Assert(commands.Contains("[ShortcutCommand(\"门板抽面\", \"生成立面门板\", \"DCMB\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DCMB\", CommandFlags.Modal)]", StringComparison.Ordinal) &&
           commands.Contains("GenerateElevationDoor();", StringComparison.Ordinal) &&
           panel.Contains("生成立面门板 (DCMB)", StringComparison.Ordinal),
        "门板入口没有统一使用不冲突且不依赖LSP加载顺序的 DCMB 命令。" );
    Assert(commands.Contains("TryPromptTwoPointBounds(editor, out var innerBounds)", StringComparison.Ordinal) &&
           commands.Contains("AcadApplication.PreTranslateMessage += OnPreTranslateMessage", StringComparison.Ordinal) &&
           commands.Contains("while (true)", StringComparison.Ordinal),
        "门板命令缺少默认两点取框、免回车门数热键或连续生成流程。" );
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.DoorPanelCommands))", StringComparison.Ordinal),
        "门板绘图命令类没有注册到插件。" );
    Assert(palette.Contains("DoorPanels", StringComparison.Ordinal),
        "工具箱模块枚举缺少门板抽面。" );
}

static void DoorPanelSurfaceUsesFixedDefaultCountAndGap()
{
    var defaults = DoorPanelOptions.Default;
    Assert(defaults.DoorCount == 2 && defaults.Gap == 3,
        "立面门板固定默认值必须是2扇门和3mm门缝。");

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelPath + ".cs");
    var doorPanelStart = panel.IndexOf("x:Name=\"DoorPanelModuleView\"", StringComparison.Ordinal);
    var planDoorPanelStart = panel.IndexOf("<TabItem Header=\"俯视门板\">", doorPanelStart, StringComparison.Ordinal);
    var hardwareStart = panel.IndexOf("x:Name=\"HardwareModuleView\"", StringComparison.Ordinal);

    Assert(doorPanelStart >= 0 && planDoorPanelStart > doorPanelStart && hardwareStart > planDoorPanelStart,
        "无法定位立面和俯视门板的独立布局范围。");
    var elevationDoorPanel = panel[doorPanelStart..planDoorPanelStart];
    Assert(!elevationDoorPanel.Contains("DoorPanelCountBox", StringComparison.Ordinal) &&
           !elevationDoorPanel.Contains("DoorPanelGapBox", StringComparison.Ordinal) &&
           !elevationDoorPanel.Contains("Text=\"门板数\"", StringComparison.Ordinal) &&
           !elevationDoorPanel.Contains("Text=\"门缝 (mm)\"", StringComparison.Ordinal),
        "立面门板页仍显示不需要的门板数或门缝设置项。");
    Assert(panelCode.Contains("defaults.DoorCount.ToString(CultureInfo.InvariantCulture)", StringComparison.Ordinal) &&
           panelCode.Contains("defaults.Gap.ToString(\"0.###\", CultureInfo.InvariantCulture)", StringComparison.Ordinal),
        "门板参数读取没有使用核心层固定默认门板数和门缝。");
}

static void HardwareHingeTabSharesDoorPanelParameters()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panelCodePath = panelPath + ".cs";
    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelCodePath);
    var hardwareStart = panel.IndexOf("x:Name=\"HardwareModuleView\"", StringComparison.Ordinal);

    Assert(panel.Contains("<GroupBox Header=\"铰链设置\" Style=\"{StaticResource DoorPanelCompactGroupStyle}\">", StringComparison.Ordinal),
        "门板抽面模块缺少铰链参数分组。" );
    Assert(hardwareStart >= 0, "无法定位五金件模块。" );
    var hardware = panel[hardwareStart..];
    Assert(hardware.Contains("<TabItem Header=\"铰链设置\">", StringComparison.Ordinal),
        "五金件模块缺少铰链设置页签。" );

    foreach (var sourceName in new[]
             {
                 "HingeTopOffsetBox", "HingeBottomOffsetBox",
                 "HingeCountBelow900Box", "HingeCount900To1500Box",
                 "HingeCount1500To2000Box", "HingeCount2000To2500Box",
                 "HingeCountAtLeast2500Box", "UseFixedHingeSpacingCheck",
                 "FixedHingeSpacingBox", "GenerateHingeDimensionsCheck",
                 "HingeDimensionModeCombo", "HingeDimensionOffsetBox",
                 "HingeDimensionLineSpacingBox", "HingeDimensionTextHeightBox",
                 "HingeTypeTextHeightBox", "HingeTypeTextSpacingBox"
             })
    {
        Assert(hardware.Contains($"ElementName={sourceName}", StringComparison.Ordinal),
            $"五金铰链页没有双向共享参数：{sourceName}。" );
    }

    Assert(hardware.Contains("Click=\"OnSaveHardwareHingeOptionsClick\"", StringComparison.Ordinal) &&
           hardware.Contains("Click=\"OnHardwareInsertHingesClick\"", StringComparison.Ordinal) &&
           hardware.Contains("Click=\"OnHardwareLabelHingesClick\"", StringComparison.Ordinal),
        "五金铰链页缺少保存、插入或标记操作入口。" );
    Assert(panelCode.Contains("OnSaveHardwareHingeOptionsClick", StringComparison.Ordinal) &&
           panelCode.Contains("OnHardwareInsertHingesClick", StringComparison.Ordinal) &&
           panelCode.Contains("OnHardwareLabelHingesClick", StringComparison.Ordinal),
        "五金铰链页操作尚未接入运行时设置与命令。" );
}

static void DoorPanelSurfaceUsesCompactLayout()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panel = File.ReadAllText(panelPath);
    var resourcesStart = panel.IndexOf("<UserControl.Resources>", StringComparison.Ordinal);
    var resourcesEnd = panel.IndexOf("</UserControl.Resources>", StringComparison.Ordinal);
    var doorPanelStart = panel.IndexOf("x:Name=\"DoorPanelModuleView\"", StringComparison.Ordinal);
    var hardwareStart = panel.IndexOf("x:Name=\"HardwareModuleView\"", StringComparison.Ordinal);

    Assert(doorPanelStart >= 0 && hardwareStart > doorPanelStart,
        "无法定位门板抽面模块的独立布局范围。" );
    Assert(resourcesStart >= 0 && resourcesEnd > resourcesStart,
        "Panel-level resources could not be located.");
    var panelResources = panel[resourcesStart..resourcesEnd];
    var doorPanel = panel[doorPanelStart..hardwareStart];
    foreach (var styleKey in new[]
             {
                 "DoorPanelCompactGroupStyle",
                 "DoorPanelCompactInputStyle",
                 "DoorPanelCompactComboStyle",
                 "DoorPanelCompactActionStyle"
             })
    {
        Assert(panelResources.Contains($"x:Key=\"{styleKey}\"", StringComparison.Ordinal),
            $"紧凑样式未定义在面板级资源中：{styleKey}。" );
        Assert(doorPanel.Contains($"StaticResource {styleKey}", StringComparison.Ordinal),
            $"门板抽面模块未使用紧凑样式：{styleKey}。" );
    }

    Assert(doorPanel.Contains("x:Name=\"DoorPanelActionGrid\"", StringComparison.Ordinal) &&
           doorPanel.Contains("<ColumnDefinition Width=\"*\" />", StringComparison.Ordinal),
        "门板操作按钮尚未收敛为紧凑的双列操作区。" );
}

static void PlanDoorPanelOptionsAreIndependentAndValidated()
{
    var defaults = PlanDoorPanelOptions.Default;
    Assert(defaults.CalculationMode == DoorPanelCalculationMode.GapFixedDoorDistributed &&
           defaults.DoorCount == 2 && defaults.Thickness == 18 && defaults.FrontClearance == 2 && defaults.Gap == 3 &&
           defaults.LeftExtension == 0 && defaults.RightExtension == 0,
        "俯视门板默认参数必须独立保持18mm厚、2mm前沿间距、2扇和3mm目标门缝。" );
    Assert(PlanDoorPanelOptions.TryCreate(
            DoorPanelCalculationMode.GapFixedDoorDistributed,
            "3", "20", "2.5", "3.2", "10", "15",
            out var parsed,
            out var error) &&
           parsed.DoorCount == 3 && parsed.Thickness == 20 && parsed.FrontClearance == 2.5 && parsed.Gap == 3.2 &&
           parsed.LeftExtension == 10 && parsed.RightExtension == 15,
        $"任意有效门缝或前沿间距未通过校验：{error}" );
    Assert(!PlanDoorPanelOptions.TryCreate(
            DoorPanelCalculationMode.GapFixedDoorDistributed,
            "2", "0", "2", "3", "0", "0",
            out _, out var thicknessError) &&
           thicknessError.Contains("门板厚度", StringComparison.Ordinal),
        "俯视门板没有拒绝零厚度。" );
}

static void PlanDoorPanelLayoutUsesCursorSideAndFlip()
{
    var options = PlanDoorPanelOptions.Default with
    {
        LeftExtension = 10,
        RightExtension = 20
    };
    var below = PlanDoorPanelLayoutCalculator.Calculate(
        new PlanDoorPanelPoint(0, 0),
        new PlanDoorPanelPoint(1000, 0),
        new PlanDoorPanelPoint(500, -100),
        options,
        flipDirection: false);

    Assert(Math.Abs(below.ActualGap - (3d + 1d / 3d)) < 0.000001 && below.Panels.Count == 2,
        "俯视门板没有应用门扇数补偿门缝。" );
    Assert(below.Panels[0].InnerStart == new PlanDoorPanelPoint(-10, -2) &&
           Math.Abs(below.Panels[0].InnerEnd.X - 503.3333333333333) < 0.000001 &&
           below.Panels[0].InnerEnd.Y == -2 &&
           below.Panels[0].OuterStart == new PlanDoorPanelPoint(-10, -20) &&
           Math.Abs(below.Panels[1].InnerStart.X - 506.6666666666667) < 0.000001 &&
           below.Panels[1].InnerStart.Y == -2 &&
           below.Panels[1].InnerEnd == new PlanDoorPanelPoint(1020, -2),
        "向右基准线且鼠标位于下方时，前沿间距、门板位置、厚度、盖值或补偿门缝不正确。" );

    var above = PlanDoorPanelLayoutCalculator.Calculate(
        new PlanDoorPanelPoint(0, 0),
        new PlanDoorPanelPoint(1000, 0),
        new PlanDoorPanelPoint(500, -100),
        options,
        flipDirection: true);
    Assert(above.Panels.All(panel => panel.InnerStart.Y == 2 && panel.OuterStart.Y == 20 && panel.OuterEnd.Y == 20),
        "R键翻转没有把向下门板镜像到柜体线上方。" );

    var vertical = PlanDoorPanelLayoutCalculator.Calculate(
        new PlanDoorPanelPoint(0, 1000),
        new PlanDoorPanelPoint(0, 0),
        new PlanDoorPanelPoint(-100, 500),
        PlanDoorPanelOptions.Default,
        flipDirection: false);
    Assert(vertical.Panels.All(panel => panel.InnerStart.X == -2 && panel.OuterStart.X == -20 && panel.OuterEnd.X == -20),
        "俯视门板没有支持向下基准线及鼠标左侧生成。" );

    var customGap = PlanDoorPanelLayoutCalculator.Calculate(
        new PlanDoorPanelPoint(0, 0),
        new PlanDoorPanelPoint(1000, 0),
        new PlanDoorPanelPoint(500, -100),
        PlanDoorPanelOptions.Default with { Gap = 3.2 },
        flipDirection: false);
    Assert(Math.Abs(customGap.ActualGap - (3.2 + 1d / 3d)) < 0.000001,
        "自由输入的非0.5档门缝没有叠加门扇数补偿。" );
}

static void PlanDoorPanelSurfaceAndCommandAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelPath + ".cs");
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PlanDoorPanelCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    Assert(panel.Contains("<TabItem Header=\"俯视门板\">", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"PlanDoorPanelThicknessBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"PlanDoorPanelGapBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"PlanDoorPanelFrontClearanceBox\"", StringComparison.Ordinal) &&
           panel.Contains("IsEditable=\"True\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"PlanDoorPanelActualGapText\"", StringComparison.Ordinal) &&
           panel.Contains("生成俯视门板 (DCMBF)", StringComparison.Ordinal),
        "门板抽面缺少俯视门板参数、实际门缝或生成入口。" );
    Assert(panelCode.Contains("PlanDoorPanelOptions.TryCreate", StringComparison.Ordinal) &&
           panelCode.Contains("PlanDoorPanelRuntimeSettings.Current", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_PLAN_DOOR", StringComparison.Ordinal),
        "俯视门板面板没有独立读取保存参数并调用底层命令。" );
    Assert(commands.Contains("[CommandMethod(\"DC_CMD_PLAN_DOOR\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"门板抽面\", \"生成俯视门板\", \"DCMBF\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DCMBF\"", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyR", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyW", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyS", StringComparison.Ordinal),
        "DCMBF缺少底层命令、原生命令或R/W/S免回车热键。" );
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.PlanDoorPanelCommands))", StringComparison.Ordinal),
        "俯视门板命令类没有注册到插件。" );
}

static void DrawerFaceOptionsUseConfirmedDefaults()
{
    var defaults = DrawerFaceOptions.Default;
    Assert(defaults.RowCount == 2 && defaults.ColumnCount == 1 &&
           defaults.Gap == 2 && defaults.Handleless && defaults.HandlelessGap == 26 &&
           defaults.HandlelessProfileMode == HandlelessProfileMode.Automatic &&
           defaults.HandlelessProfileSize == 50 &&
           defaults.GenerateOpeningLines && defaults.GenerateMask && !defaults.GenerateHandle,
        "抽面必须默认两行一列、2mm普通缝、26mm免拉手上下缝、50mm侧视图挡板，并开启向下开门线和背景遮罩。" );
    Assert(DrawerFaceOptions.TryCreateGrid(
            "3", "2", "2.5", "28", "10", "20", "30", "40", true, true, true,
            out var parsed,
            out var error) &&
           parsed.RowCount == 3 && parsed.ColumnCount == 2 && parsed.Gap == 2.5 &&
           parsed.Handleless && parsed.HandlelessGap == 28 && parsed.GenerateOpeningLines &&
           parsed.TopExtension == 10 && parsed.BottomExtension == 20 &&
           parsed.LeftExtension == 30 && parsed.RightExtension == 40 &&
           parsed.GenerateMask && !parsed.GenerateHandle,
        $"有效抽面参数未通过验证：{error}" );
    Assert(!DrawerFaceOptions.TryCreateGrid(
            "0", "1", "2", "26", "0", "0", "0", "0", true, true, true,
            out _, out var rowError) && rowError.Contains("行数", StringComparison.Ordinal),
        "抽面没有拒绝小于1的行数。" );
    Assert(!DrawerFaceOptions.TryCreateGrid(
            "2", "0", "2", "26", "0", "0", "0", "0", true, true, true,
            out _, out var columnError) && columnError.Contains("列数", StringComparison.Ordinal),
        "抽面没有拒绝小于1的列数。" );

    var legacyPath = Path.Combine(Path.GetTempPath(), $"drawer-face-legacy-{Guid.NewGuid():N}.json");
    try
    {
        File.WriteAllText(
            legacyPath,
            "{\"Version\":1,\"DrawerFace\":{\"Orientation\":1,\"Count\":3,\"Gap\":4,\"TopExtension\":0," +
            "\"BottomExtension\":0,\"LeftExtension\":0,\"RightExtension\":0,\"GenerateMask\":false,\"GenerateHandle\":true}}");
        var migrated = new ToolboxSettingsStore(legacyPath).Load().DrawerFace;
        Assert(migrated.RowCount == 1 && migrated.ColumnCount == 3 && migrated.Gap == 2 &&
               migrated.Handleless && migrated.HandlelessGap == 26 && migrated.GenerateOpeningLines &&
               migrated.GenerateMask && !migrated.GenerateHandle,
            "旧版单方向抽面设置没有迁移为二维行列、2mm普通缝、26mm免拉手缝和默认遮罩。" );
    }
    finally
    {
        File.Delete(legacyPath);
    }
}

static void DrawerFaceLayoutSupportsVerticalAndHorizontalDirections()
{
    var grid = DrawerFaceLayoutCalculator.Calculate(
        new DrawerFacePoint(100, 200),
        new DrawerFacePoint(900, 1200),
        DrawerFaceOptions.Default with
        {
            RowCount = 2,
            ColumnCount = 2,
            TopExtension = 20,
            BottomExtension = 10,
            LeftExtension = 5,
            RightExtension = 15
        });
    Assert(grid.Faces.Count == 4 && grid.RowCount == 2 && grid.ColumnCount == 2 &&
           grid.ActualHorizontalGap == 2 && grid.ActualVerticalGap == 26 &&
           grid.FaceWidth == 409 && grid.FaceHeight == 502,
        "二维抽面没有按2mm左右缝和26mm免拉手上下缝精确均分。" );
    Assert(grid.Faces[0] == new DrawerFaceRectangle(95, 190, 409, 502) &&
           grid.Faces[1] == new DrawerFaceRectangle(506, 190, 409, 502) &&
           grid.Faces[2] == new DrawerFaceRectangle(95, 718, 409, 502) &&
           grid.Faces[3].Right == 915 && grid.Faces[3].Top == 1220,
        "二维抽面的四向盖值、行列顺序或最终收口不正确。" );

    var ordinaryGap = DrawerFaceLayoutCalculator.Calculate(
        new DrawerFacePoint(0, 0),
        new DrawerFacePoint(1000, 600),
        DrawerFaceOptions.Default with
        {
            RowCount = 2,
            ColumnCount = 3,
            Gap = 4,
            Handleless = false
        });
    Assert(ordinaryGap.Faces.Count == 6 && ordinaryGap.ActualHorizontalGap == 4 &&
           ordinaryGap.ActualVerticalGap == 4 &&
           Math.Abs(ordinaryGap.Faces[2].Right - 1000) < 0.000001 &&
           Math.Abs(ordinaryGap.Faces[5].Top - 600) < 0.000001,
        "关闭免拉手后没有在横向和竖向都使用普通抽面缝，或最终边界未精确收口。" );
}

static void HandlelessProfileDetectsRealBoundariesAndBuildsProfiles()
{
    AxisAlignedSegment[] middleSegments =
    [
        new(100, 113, 118, 113),
        new(118, 87, 100, 87),
        new(118, 113, 118, 300),
        new(118, -100, 118, 87)
    ];
    Assert(HandlelessProfileCalculator.TryDetect(
            new BoardPlacementPoint(100, 113),
            middleSegments,
            HandlelessProfileMode.Automatic,
            18,
            50,
            26,
            out var middleAnchor),
        "点击中拉手门板边线端点后没有识别26mm真实缝隙。" );
    Assert(middleAnchor.Type == HandlelessProfileMode.Middle &&
           middleAnchor.UpperBoundaryY == 113 && middleAnchor.LowerBoundaryY == 87,
        "中拉手没有采用上下门板真实边界。" );

    var middleRight = HandlelessProfileCalculator.Calculate(
        middleAnchor,
        HandlelessProfileFrontSide.Right,
        18,
        50);
    Assert(middleRight.Boards.Count == 3 && middleRight.DoorOverlap == 12,
        "50挡板和26免拉手缝没有自动反算12mm搭接或生成C型三块板。" );
    Assert(middleRight.Boards[0] == new HandlelessProfileRectangle(50, 125, 50, 18) &&
           middleRight.Boards[1] == new HandlelessProfileRectangle(50, 75, 18, 50) &&
           middleRight.Boards[2] == new HandlelessProfileRectangle(50, 57, 50, 18),
        "右侧柜前的中拉手C型位置不正确。" );

    var middleLeft = HandlelessProfileCalculator.Calculate(
        middleAnchor,
        HandlelessProfileFrontSide.Left,
        18,
        50);
    Assert(middleLeft.Boards[0].X == 118 && middleLeft.Boards[1].X == 150,
        "左侧柜前没有把C型免拉手镜像到门板内侧。" );

    AxisAlignedSegment[] topSegments =
    [
        new(0, 126, 118, 126),
        new(100, 100, 118, 100),
        new(118, 100, 118, -200)
    ];
    Assert(HandlelessProfileCalculator.TryDetect(
            new BoardPlacementPoint(109, 120),
            topSegments,
            HandlelessProfileMode.Automatic,
            18,
            50,
            26,
            out var topAnchor),
        "点击顶部柜板和门板之间后没有识别上拉手。" );
    Assert(topAnchor.Type == HandlelessProfileMode.Top &&
           topAnchor.UpperBoundaryY == 126 && topAnchor.LowerBoundaryY == 100,
        "上拉手没有采用顶部柜板与门板真实边界。" );

    var top = HandlelessProfileCalculator.Calculate(
        topAnchor,
        HandlelessProfileFrontSide.Right,
        18,
        50);
    Assert(top.Boards.Count == 2 && top.DoorOverlap == 24 &&
           top.Boards[0] == new HandlelessProfileRectangle(50, 76, 18, 50) &&
           top.Boards[1] == new HandlelessProfileRectangle(50, 58, 50, 18),
        "上拉手没有从顶板内侧边生成L型两块50挡板。" );

    AxisAlignedSegment[] doubleLineTopSegments =
    [
        new(0, 144, 118, 144),
        new(0, 126, 118, 126),
        new(100, 100, 118, 100)
    ];
    Assert(HandlelessProfileCalculator.TryDetect(
            new BoardPlacementPoint(109, 113),
            doubleLineTopSegments,
            HandlelessProfileMode.Automatic,
            18,
            50,
            26,
            out var doubleLineTopAnchor),
        "双线顶板下方没有识别到上拉手。" );
    Assert(doubleLineTopAnchor.Type == HandlelessProfileMode.Top &&
           doubleLineTopAnchor.UpperBoundaryY == 126,
        "双线顶板错误地采用了外侧边，50竖挡板会与18厚顶板重叠。" );
    var doubleLineTop = HandlelessProfileCalculator.Calculate(
        doubleLineTopAnchor,
        HandlelessProfileFrontSide.Right,
        18,
        50);
    Assert(doubleLineTop.Boards[0].Top == 126,
        "上拉手L型竖挡板越过顶板内侧边并占用了顶板本体。" );

    Assert(!HandlelessProfileCalculator.TryDetect(
            new BoardPlacementPoint(500, 500),
            middleSegments,
            HandlelessProfileMode.Automatic,
            18,
            50,
            26,
            out _),
        "远离缝隙的点击不应误识别免拉手侧视图。" );
}

static void DrawerFaceSurfaceAndCommandAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelPath + ".cs");
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DrawerFaceCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));
    var runtime = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelRuntimeSettings.cs"));

    Assert(panel.Contains("<TabItem Header=\"抽面设置\">", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceRowCountBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceColumnCountBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceGapBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceHandlelessCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceHandlelessGapBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceHandlelessProfileModeCombo\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceHandlelessProfileSizeBox\"", StringComparison.Ordinal) &&
           panel.Contains("生成免拉手侧视图 (DCMLS)", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceGenerateOpeningLinesCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceGenerateMaskCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceActualSizeText\"", StringComparison.Ordinal) &&
           panel.Contains("生成抽面 (DCMC)", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"DrawerFaceCalculationModeCombo\"", StringComparison.Ordinal) &&
           !panel.Contains("拾取拉手 (DCSDH)", StringComparison.Ordinal),
        "门板抽面模块缺少行列数、免拉手、开门线、遮罩、实际尺寸或生成入口，或仍残留无关拉手设置。" );
    Assert(panelCode.Contains("DrawerFaceOptions.TryCreateGrid", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_DRAWER_FACE", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_DRAWER_HANDLELESS_PROFILE", StringComparison.Ordinal),
        "抽面面板没有读取二维独立参数或调用抽面、免拉手侧视图底层命令。" );
    Assert(commands.Contains("[CommandMethod(\"DC_CMD_DRAWER_FACE\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"门板抽面\", \"生成抽面\", \"DCMC\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DCMC\"", StringComparison.Ordinal) &&
           commands.Contains("BoardCommands.TryGetBoardSpaceBounds", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyW", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyS", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyA", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyD", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyUp", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyDown", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyLeft", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyRight", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyAdd", StringComparison.Ordinal) &&
           commands.Contains("Wipeout", StringComparison.Ordinal),
        "DCMC缺少智能识别、二维行列热键或遮罩落图。" );
    Assert(commands.Contains("[CommandMethod(\"DC_CMD_DRAWER_HANDLELESS_PROFILE\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"门板抽面\", \"生成免拉手侧视图\", \"DCMLS\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DCMLS\"", StringComparison.Ordinal) &&
           commands.Contains("HandlelessProfileCalculator.TryDetect", StringComparison.Ordinal) &&
           commands.Contains("CadAxisAlignedSegmentReader.CollectCurrentSpace", StringComparison.Ordinal) &&
           commands.Contains("TryGetManualHandlelessAnchor", StringComparison.Ordinal),
        "DCMLS缺少自动识别、左右预览或手动定位兜底。" );
    Assert(runtime.Contains("class DrawerFaceRuntimeSettings", StringComparison.Ordinal) &&
           application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.DrawerFaceCommands))", StringComparison.Ordinal),
        "抽面运行参数或命令类没有注册到插件。" );
}

static void DrawerFacePreviewAndReleaseConfigurationAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DrawerFaceCommands.cs"));
    var solution = File.ReadAllText(Path.Combine(root, "CADDC.sln"));
    var publishIndex = commands.IndexOf("DrawerFaceRuntimeSettings.PublishLayout(initialLayout)", StringComparison.Ordinal);
    var dragIndex = commands.IndexOf("editor.Drag(jig)", StringComparison.Ordinal);
    const string coreProjectGuid = "{F67B729E-6F8A-4C41-A9F1-4B76D5A5C3F8}";

    Assert(publishIndex >= 0 && dragIndex > publishIndex,
        "首次抽面预览没有在拖拽确认前发布实际尺寸。");
    Assert(solution.Contains($"\"YiZhiMao.Dc.Core\", \"src\\YiZhiMao.Dc.Core\\YiZhiMao.Dc.Core.csproj\", \"{coreProjectGuid}\"", StringComparison.Ordinal) &&
           solution.Contains($"{coreProjectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU", StringComparison.Ordinal) &&
           solution.Contains($"{coreProjectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU", StringComparison.Ordinal) &&
           solution.Contains($"{coreProjectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU", StringComparison.Ordinal) &&
           solution.Contains($"{coreProjectGuid}.Release|Any CPU.Build.0 = Release|Any CPU", StringComparison.Ordinal),
        "解决方案没有为 Core 项目配置完整的 Debug/Release 构建映射。");
}

static void DrawerFaceOpeningArrowAndHotkeysFollowDrawingWorkflow()
{
    var arrow = DrawerFaceLayoutCalculator.CalculateOpeningArrow(
        new DrawerFaceRectangle(100, 200, 600, 300));
    Assert(arrow.Count == 2,
        "抽面开门线没有使用与门板一致的两条虚线。" );
    var tip = arrow[0].End;
    Assert(tip == new DrawerFacePoint(400, 200) && arrow[1].End == tip &&
           arrow[0].Start == new DrawerFacePoint(100, 500) &&
           arrow[1].Start == new DrawerFacePoint(700, 500),
        "抽面开门线没有从两个上角向下边中心汇合。" );

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DrawerFaceCommands.cs"));
    Assert(commands.Contains("DrawerFaceLayoutCalculator.CalculateOpeningArrow(face)", StringComparison.Ordinal) &&
           commands.Contains("private const string OpeningLayerName = \"DC_开门线\"", StringComparison.Ordinal),
        "抽面正式写入流程没有落图向下开门箭头。" );
    Assert(commands.Contains("VirtualKeyW or VirtualKeyUp => 1", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyS or VirtualKeyDown => -1", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyD or VirtualKeyRight => 1", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyA or VirtualKeyLeft => -1", StringComparison.Ordinal) &&
           commands.Contains("jig.AdjustRows(rowDelta)", StringComparison.Ordinal) &&
           commands.Contains("jig.AdjustColumns(columnDelta)", StringComparison.Ordinal),
        "W/S、上/下、加减键没有控制行数，或A/D、左/右键没有控制列数。" );
}

static void DoorColorMaskAndDrawingLayerSurfaceAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DrawingLayerCommands.cs"));
    var handles = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "HandleCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    Assert(panel.Contains("x:Name=\"DoorPanelColorBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"PlanDoorPanelColorBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceColorBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DoorPanelGenerateMaskCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawingLayerModuleView\"", StringComparison.Ordinal),
        "门板线色、背景遮罩或图层图纸模块界面不完整。" );
    Assert(commands.Contains("DC_CMD_LAYER_STANDARDIZE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LAYER_NORMALIZE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_DRAWING_DISPLAY_REPAIR", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LAYER_SHOW_ALL", StringComparison.Ordinal),
        "图层规范、归层、隔离或显示修复命令不完整。" );
    Assert(handles.Contains("DC_CMD_HANDLE_PICK", StringComparison.Ordinal) &&
           handles.Contains("DC_CMD_HANDLE_APPLY", StringComparison.Ordinal) &&
           application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.HandleCommands))", StringComparison.Ordinal) &&
           application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.DrawingLayerCommands))", StringComparison.Ordinal),
        "拉手工作流或图层图纸命令类没有注册。" );
}

static void DrawingDisplayRepairTraversesAllLocalBlocksAndReevaluatesHatches()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DrawingLayerCommands.cs"));

    Assert(commands.Contains("foreach (ObjectId blockId in blocks)", StringComparison.Ordinal) &&
           commands.Contains("block.IsFromExternalReference", StringComparison.Ordinal) &&
           commands.Contains("IsWallHatchLayer(hatch.Layer)", StringComparison.Ordinal) &&
           commands.Contains("hatch.EvaluateHatch(true)", StringComparison.Ordinal),
        "TXF 仍只扫描当前空间，或修改比例后没有重新计算填充。" );
}

static void DrawingLayerOptionsPersistMappingsAndSelectionLayers()
{
    var defaults = DrawingLayerToolOptions.Default;
    Assert(defaults.CategoryLayers[DrawingLayerCategories.ActivityShelf] == "0-柜体结构" &&
           defaults.CategoryLayers[DrawingLayerCategories.VisiblePanel] == "0-柜体虚线" &&
           defaults.CategoryLayers[DrawingLayerCategories.DoorPanel] == "0-门板图层" &&
           defaults.DoorSelectionLayer == "0-门板图层" &&
           defaults.CabinetSelectionLayer == "0-柜体结构" &&
           defaults.CustomSelectionLayerOne == "0-五金" &&
           defaults.CustomSelectionLayerTwo == "0-文字",
        "图层分类映射或四组智能选择默认图层不正确。" );
    var isolatedDefault = ToolboxSettings.CreateDefault();
    isolatedDefault.DrawingLayer.CategoryLayers[DrawingLayerCategories.ActivityShelf] = "A-临时";
    Assert(ToolboxSettings.CreateDefault().DrawingLayer.CategoryLayers[DrawingLayerCategories.ActivityShelf] == "0-柜体结构",
        "不同工具箱默认实例仍共享可变图层映射。" );

    var directory = Path.Combine(Path.GetTempPath(), "YiZhiMaoDcTests", Guid.NewGuid().ToString("N"));
    var path = Path.Combine(directory, "ToolboxSettings.json");
    try
    {
        var settings = ToolboxSettings.CreateDefault();
        settings.DrawingLayer = defaults with
        {
            CategoryLayers = new Dictionary<string, string>(defaults.CategoryLayers, StringComparer.Ordinal)
            {
                ["自定义层板"] = "A-层板"
            },
            DoorSelectionLayer = "A-门板",
            CustomSelectionLayerOne = "A-五金"
        };
        new ToolboxSettingsStore(path).Save(settings);

        var loaded = new ToolboxSettingsStore(path).Load();
        Assert(loaded.DrawingLayer.CategoryLayers["自定义层板"] == "A-层板" &&
               loaded.DrawingLayer.DoorSelectionLayer == "A-门板" &&
               loaded.DrawingLayer.CustomSelectionLayerOne == "A-五金",
            "自定义图层映射或智能选择图层没有持久化。" );

        var clone = loaded.Clone();
        clone.DrawingLayer.CategoryLayers["自定义层板"] = "B-层板";
        Assert(loaded.DrawingLayer.CategoryLayers["自定义层板"] == "A-层板",
            "工具箱克隆仍共享图层映射字典，会污染运行时设置。" );
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

static void DrawingLayerSurfaceContainsConfigurationAndSelectionTools()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var codeBehind = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));

    Assert(panel.Contains("Header=\"自定义图层配置\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawingLayerMappingRowsPanel\"", StringComparison.Ordinal) &&
           panel.Contains("OnLoadDrawingLayerNamesClick", StringComparison.Ordinal) &&
           panel.Contains("OnSaveDrawingLayerConfigurationClick", StringComparison.Ordinal) &&
           panel.Contains("Header=\"快捷图层与图形工具\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"智能选择工具\"", StringComparison.Ordinal),
        "图层图纸面板缺少自定义配置、实体控制或智能选择分组。" );
    Assert(codeBehind.Contains("BuildDrawingLayerMappingRows", StringComparison.Ordinal) &&
           codeBehind.Contains("ReadDrawingLayerConfiguration", StringComparison.Ordinal) &&
           codeBehind.Contains("DrawingLayerRuntimeSettings.Save", StringComparison.Ordinal),
        "图层配置界面没有加载、读取并保存统一运行时参数。" );
}

static void DrawingLayerEntityControlCommandsUseDcPrefixes()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DrawingLayerCommands.cs"));
    foreach (var command in new[]
             {
                 "DC_CMD_LAYER_MAKE_CURRENT", "DC_CMD_ENTITY_HIDE", "DC_CMD_ENTITY_UNHIDE_LAST",
                 "DC_CMD_ENTITY_SHOW_ALL", "DC_CMD_ENTITY_LOCK_LAYERS", "DC_CMD_ENTITY_UNLOCK_LAYERS",
                 "DC_CMD_ENTITY_UNLOCK_ALL_LAYERS"
             })
    {
        Assert(commands.Contains(command, StringComparison.Ordinal), $"缺少实体控制底层命令 {command}。" );
    }

    foreach (var alias in new[] { "DCT1", "DCH1", "DCH2", "DCH3", "DCS1", "DCS2", "DCS3" })
    {
        Assert(commands.Contains($"\"{alias}\"", StringComparison.Ordinal), $"实体控制快捷键 {alias} 未使用 DC 前缀。" );
    }

    Assert(commands.Contains("writable.Visible = visible", StringComparison.Ordinal) &&
           commands.Contains("writable.Visible = true", StringComparison.Ordinal) &&
           commands.Contains("OpenMode.ForWrite, false, true", StringComparison.Ordinal) &&
           commands.Contains("layer.IsLocked = locked", StringComparison.Ordinal),
        "隐藏/显示或图层锁定没有落到 AutoCAD 实体和图层状态。" );
}

static void DrawingLayerSmartSelectionUsesConfiguredLayersAndObjectTraits()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DrawingLayerCommands.cs"));
    foreach (var command in new[]
             {
                 "DC_CMD_SELECT_DOOR_LAYER", "DC_CMD_SELECT_CABINET_LAYER", "DC_CMD_SELECT_CUSTOM_LAYER_ONE",
                 "DC_CMD_SELECT_CUSTOM_LAYER_TWO", "DC_CMD_SELECT_ALL_ANNOTATIONS", "DC_CMD_SELECT_SIMILAR_IN_RANGE"
             })
    {
        Assert(commands.Contains(command, StringComparison.Ordinal), $"缺少智能选择底层命令 {command}。" );
    }

    Assert(commands.Contains("DrawingLayerRuntimeSettings.DoorSelectionLayer", StringComparison.Ordinal) &&
           commands.Contains("GetXDataForApplication(BoardCommands.BoardPropertyAppName)", StringComparison.Ordinal) &&
           commands.Contains("DrawingLayerRuntimeSettings.Current.CategoryLayers.Values", StringComparison.Ordinal) &&
           commands.Contains("new SelectionFilter", StringComparison.Ordinal) &&
           commands.Contains("DxfCode.LayerName", StringComparison.Ordinal) &&
           commands.Contains("SetImpliedSelection", StringComparison.Ordinal) &&
           commands.Contains("GetRXClass()", StringComparison.Ordinal) &&
           commands.Contains("string.Equals(candidate.Layer, reference.Layer", StringComparison.Ordinal),
        "智能选择没有使用持久化图层过滤，或类似对象没有按类型和图层匹配。" );
}

static void BoardModuleSurfaceAndCommandsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "BoardCommands.cs"));
    var radialMenu = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Core", "RadialMenuOptions.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    Assert(panel.Contains("x:Name=\"BoardModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"板件\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"单双线\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"分区\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"组件\"", StringComparison.Ordinal),
        "板件主模块或四个已确认页签不完整。" );
    var lineTab = panel.IndexOf("Header=\"单双线\"", StringComparison.Ordinal);
    var partitionTab = panel.IndexOf("Header=\"分区\"", StringComparison.Ordinal);
    var componentTab = panel.IndexOf("Header=\"组件\"", StringComparison.Ordinal);
    Assert(lineTab >= 0 && partitionTab > lineTab && componentTab > partitionTab,
        "分区页签没有放在单双线与组件之间。" );
    Assert(commands.Contains("DC_CMD_BOARD_DRAW", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_BOARD_CENTER", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_SINGLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_DOUBLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_TRIPLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_TO_DOUBLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_TO_TRIPLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_DOOR_PARTITION_GUIDE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_SHOE_SHELVES", StringComparison.Ordinal),
        "板件、单双线或组件底层命令入口不完整。" );
    Assert(panel.Contains("x:Name=\"ShoeShelfDepthBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"ShoeShelfSmartAngleRadio\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"ShoeShelfGenerateStopperCheck\"", StringComparison.Ordinal) &&
           panel.Contains("生成鞋柜斜层板 (DCXB)", StringComparison.Ordinal) &&
           !panel.Contains("WineRack", StringComparison.Ordinal) &&
           !panel.Contains("十字酒架", StringComparison.Ordinal) &&
           !panel.Contains("45°斜酒架", StringComparison.Ordinal) &&
           !commands.Contains("[ShortcutCommand(\"板件\", \"生成十字酒架\"", StringComparison.Ordinal) &&
           !commands.Contains("[ShortcutCommand(\"板件\", \"生成斜酒架\"", StringComparison.Ordinal) &&
           BoardToolOptions.Default.ShoeShelfDepth == 260 &&
           BoardToolOptions.Default.ShoeShelfSmartAngle &&
           BoardToolOptions.Default.ShoeShelfAngleDegrees == 20 &&
           BoardToolOptions.Default.ShoeShelfTargetSpacing == 200 &&
           BoardToolOptions.Default.ShoeShelfFrontClearance == 20 &&
           BoardToolOptions.Default.ShoeShelfStopperHeight == 20 &&
           BoardToolOptions.Default.ShoeShelfGenerateStopper,
        "组件页没有完整替换为带默认前挡条的智能鞋柜斜层板。" );
    var shoeCommandStart = commands.IndexOf("[CommandMethod(\"DC_CMD_SHOE_SHELVES\"", StringComparison.Ordinal);
    var shoeCommandEnd = commands.IndexOf("[CommandMethod(\"DC_CMD_WINE_RACK_CROSS\"", StringComparison.Ordinal);
    var shoeCommand = commands[shoeCommandStart..shoeCommandEnd];
    Assert(shoeCommand.IndexOf("ShoeShelfLayoutCalculator.ResolvePreview", StringComparison.Ordinal) >= 0 &&
           shoeCommand.IndexOf("ShoeShelfLayoutCalculator.ResolvePreview", StringComparison.Ordinal) <
           shoeCommand.IndexOf("document.Editor.Drag(jig)", StringComparison.Ordinal) &&
           shoeCommand.Contains("catch (Exception exception)", StringComparison.Ordinal),
        "鞋柜斜层板没有在启动CAD拖拽前完成有效布局预检，或拖拽异常未被安全处理。" );
    Assert(commands.Contains("BoardGeometryCalculator.GetParallelLineOffsets", StringComparison.Ordinal) &&
           commands.Contains("BoardPlacementCalculator.CalculateOffsetLines", StringComparison.Ordinal) &&
           commands.Contains("TryGetBoardSpaceBounds(document", StringComparison.Ordinal) &&
           commands.Contains("LineOffsetPreviewJig", StringComparison.Ordinal) &&
           commands.Contains("Editor.GetDouble", StringComparison.Ordinal) &&
           commands.Contains("Editor.Drag(jig)", StringComparison.Ordinal) &&
           commands.Contains("line.Erase();", StringComparison.Ordinal),
        "单双三线偏移缺少临时距离输入和鼠标预览，或单线转换没有替换原线。" );
    Assert(panel.Contains("x:Name=\"BoardConvertLineThicknessBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardConvertDoubleModeCombo\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardConvertTripleModeCombo\"", StringComparison.Ordinal) &&
           BoardToolOptions.Default.ConvertLineThickness == 18 &&
           !BoardToolOptions.Default.ConvertDoubleAsRectangle &&
           !BoardToolOptions.Default.ConvertTripleAsRectangle,
        "单线变双/三缺少独立的18mm板厚和生成线/矩形模式。" );
    Assert(panel.Contains("单线变双 (DC22)", StringComparison.Ordinal) &&
           panel.Contains("单线变三 (DC33)", StringComparison.Ordinal) &&
           commands.Contains("\"DC22\"", StringComparison.Ordinal) &&
           commands.Contains("\"DC33\"", StringComparison.Ordinal) &&
           radialMenu.Contains("\"DC22\"", StringComparison.Ordinal) &&
           radialMenu.Contains("\"DC33\"", StringComparison.Ordinal) &&
           !panel.Contains("DCZ2", StringComparison.Ordinal) &&
           !panel.Contains("DCZ3", StringComparison.Ordinal),
        "单线变双/三没有统一使用 DC22、DC33 快捷命令。" );
    Assert(panel.Contains("x:Name=\"DoorPartitionTopExtensionBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DoorPartitionBottomExtensionBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DoorPartitionMaximumWidthBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DoorPartitionDefaultCountBox\"", StringComparison.Ordinal) &&
           panel.Contains("点选两点分区 (FQ)", StringComparison.Ordinal) &&
           commands.Contains("DoorPartitionPreviewJig", StringComparison.Ordinal) &&
           commands.Contains("DoorPartitionHotkeyScope", StringComparison.Ordinal) &&
           commands.Contains("Editor.GetInteger", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyW", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyS", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyOemPlus", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyOemMinus", StringComparison.Ordinal) &&
           commands.Contains("\"FQ\"", StringComparison.Ordinal) &&
           BoardToolOptions.Default.DoorPartitionTopExtension == 2700 &&
           BoardToolOptions.Default.DoorPartitionBottomExtension == 300 &&
           BoardToolOptions.Default.DoorPartitionMaximumWidth == 450 &&
           BoardToolOptions.Default.DoorPartitionDefaultCount == 8 &&
           BoardToolOptions.Default.DoorPartitionSmartMode,
        "门板分区辅助参数、FQ命令或W/S与+/-数量调整入口不完整。" );
    Assert(commands.Contains("DoorOpeningDetector.TryDetect", StringComparison.Ordinal) &&
           commands.Contains("CadAxisAlignedSegmentReader.CollectCurrentSpace", StringComparison.Ordinal) &&
           commands.Contains("CenterBoardPreviewJig", StringComparison.Ordinal) &&
           commands.Contains("CenterBoardHotkeyScope", StringComparison.Ordinal) &&
           commands.Contains("TryGetRectangle", StringComparison.Ordinal),
        "空间中分板没有完成单点内空识别、方向预览或手动框选兜底。");
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.BoardCommands))", StringComparison.Ordinal),
        "板件命令类没有注册到插件。" );
}

static void BoardTagUsesNormalizedScaleAndLowerText()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "BoardCommands.cs"));
    var settings = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Core", "ToolboxSettings.cs"));

    Assert(BoardToolOptions.Default.TagScale == 1 &&
           panel.Contains("x:Name=\"BoardTagScaleBox\"", StringComparison.Ordinal) &&
           panel.Contains("Style=\"{StaticResource DoorPanelCompactInputStyle}\" Text=\"1\"", StringComparison.Ordinal),
        "板件标识比例的新建默认值不是1。" );
    Assert(commands.Contains("var radius = BoardRuntimeSettings.Thickness * scale;", StringComparison.Ordinal) &&
           commands.Contains("var textGap = radius / 3;", StringComparison.Ordinal) &&
           commands.Contains("HorizontalMode = TextHorizontalMode.TextCenter", StringComparison.Ordinal) &&
           commands.Contains("VerticalMode = TextVerticalMode.TextTop", StringComparison.Ordinal) &&
           commands.Contains("markerBottomY - textGap", StringComparison.Ordinal),
        "板件标识图形没有按板厚归一，或标识文字与图形的间距不足。" );
    Assert(panel.Contains("<ComboBoxItem Content=\"玻璃层板带文字\" />", StringComparison.Ordinal) &&
           commands.Contains("Compatibility.Contains(BoardRuntimeSettings.TagType, \"玻璃层板\"", StringComparison.Ordinal) &&
           commands.Contains("new Polyline(3)", StringComparison.Ordinal) &&
           commands.Contains("Closed = true", StringComparison.Ordinal),
        "板件模块没有提供玻璃层板选项，或玻璃层板没有生成闭合三角形标识。" );
    string[] orderedTagTypes =
    [
        "<ComboBoxItem Content=\"固定板件\" />",
        "<ComboBoxItem Content=\"活动层板\" />",
        "<ComboBoxItem Content=\"玻璃层板\" />",
        "<ComboBoxItem Content=\"活动层板带文字\" />",
        "<ComboBoxItem Content=\"玻璃层板带文字\" />"
    ];
    var tagTypeIndexes = orderedTagTypes.Select(value => panel.IndexOf(value, StringComparison.Ordinal)).ToArray();
    Assert(tagTypeIndexes.All(index => index >= 0) &&
           tagTypeIndexes.SequenceEqual(tagTypeIndexes.OrderBy(index => index)),
        "板件标识类型没有按固定、活动、玻璃、带文字类型排列。" );
    Assert(BoardToolOptions.Default.PropertyType == "普通板件" &&
           panel.Contains("x:Name=\"BoardPropertyNormalRadio\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardPropertySideRadio\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardPropertyTopRadio\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardPropertyVisibleRadio\"", StringComparison.Ordinal),
        "常规板件绘制没有提供四种板件属性类型，或默认值不是普通板件。" );
    Assert(settings.Contains("PropertyType", StringComparison.Ordinal) &&
           commands.Contains("BoardPropertyAppName", StringComparison.Ordinal) &&
           commands.Contains("polyline.XData = xdata", StringComparison.Ordinal),
        "板件属性类型没有持久化到设置并写入新板件对象 XData。" );
    var horizontalModeIndex = commands.IndexOf("HorizontalMode = TextHorizontalMode.TextCenter", StringComparison.Ordinal);
    var alignmentPointIndex = commands.IndexOf("text.AlignmentPoint = textAnchor;", StringComparison.Ordinal);
    Assert(horizontalModeIndex >= 0 && alignmentPointIndex > horizontalModeIndex,
        "DBText must enter centered alignment mode before setting AlignmentPoint, otherwise AutoCAD throws eNotApplicable.");
}

static void BoardPlacementUsesEdgeAnchorsAndCursorDirection()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "BoardCommands.cs"));
    var segmentReader = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "CadAxisAlignedSegmentReader.cs"));
    Assert(BoardToolOptions.Default.TrimFaceWidth == 50 &&
           BoardToolOptions.Default.TrimAuxiliaryDepth == 70 &&
           BoardToolOptions.Default.SideTrimSectionType == "L" &&
           BoardToolOptions.Default.TopTrimSectionType == "L" &&
           BoardToolOptions.Default.PlacementView == "立面",
        "收口板默认尺寸、L型截面或默认视图不正确。" );
    Assert(panel.Contains("x:Name=\"BoardTrimFaceWidthBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardTrimAuxiliaryDepthBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardSideTrimSectionTypeCombo\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardTopTrimSectionTypeCombo\"", StringComparison.Ordinal) &&
           commands.Contains("CalculatePlanSideTrim", StringComparison.Ordinal) &&
           commands.Contains("CalculatePlanTopTrim", StringComparison.Ordinal) &&
           commands.Contains("DrawPlanTrimSection(document, \"侧收口\", BoardRuntimeSettings.SideTrimSectionType, false)", StringComparison.Ordinal) &&
           commands.Contains("DrawPlanTrimSection(document, \"顶收口\", BoardRuntimeSettings.TopTrimSectionType, true)", StringComparison.Ordinal) &&
           commands.Contains("CalculatePlanVisiblePanel", StringComparison.Ordinal) &&
           commands.Contains("TryPromptPlacementView", StringComparison.Ordinal) &&
           commands.Contains("TryPromptTopTrimView", StringComparison.Ordinal) &&
           commands.Contains("TrimStartDetector.TryDetect", StringComparison.Ordinal) &&
           commands.Contains("TrimSectionHotkeyScope", StringComparison.Ordinal) &&
           commands.Contains("BoardPlacementPreviewJig", StringComparison.Ordinal) &&
           commands.Contains("DirectionDeadZone", StringComparison.Ordinal) &&
           !commands.Contains("DrawDetectedTrimSection", StringComparison.Ordinal) &&
           segmentReader.Contains("entity is Line", StringComparison.Ordinal) &&
           segmentReader.Contains("entity is not Polyline", StringComparison.Ordinal) &&
           segmentReader.Contains("entity is BlockReference reference", StringComparison.Ordinal) &&
           segmentReader.Contains("reference.BlockTransform", StringComparison.Ordinal) &&
           segmentReader.Contains("activeBlockRecords.Add", StringComparison.Ordinal) &&
           segmentReader.Contains("activeBlockRecords.Remove", StringComparison.Ordinal) &&
           segmentReader.Contains("!entity.Visible", StringComparison.Ordinal) &&
           segmentReader.Contains("!layer.IsOff && !layer.IsFrozen", StringComparison.Ordinal),
        "收口参数、设置宽度、L/T/I切换、平立面入口或CAD线段适配不完整。" );

    var right = BoardPlacementCalculator.CalculateEdgeBoard(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(0, 100),
        new BoardPlacementPoint(20, 50),
        50);
    AssertPolygon(right.Polygons[0],
        (0, 0), (0, 100), (50, 100), (50, 0));

    var left = BoardPlacementCalculator.CalculateEdgeBoard(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(0, 100),
        new BoardPlacementPoint(-20, 50),
        50);
    AssertPolygon(left.Polygons[0],
        (0, 0), (0, 100), (-50, 100), (-50, 0));

    var sideTrim = BoardPlacementCalculator.CalculatePlanSideTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(100, 100),
        50,
        70,
        18);
    Assert(sideTrim.Polygons.Count == 2, "侧收口平面没有同时生成正面板和辅助板。" );
    AssertPolygon(sideTrim.Polygons[0],
        (0, 0), (50, 0), (50, 18), (0, 18));
    AssertPolygon(sideTrim.Polygons[1],
        (0, 18), (0, 88), (18, 88), (18, 18));

    var upperLeftTrim = BoardPlacementCalculator.CalculatePlanSideTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(-100, 20),
        50,
        70,
        18);
    AssertPolygon(upperLeftTrim.Polygons[0],
        (0, 0), (-50, 0), (-50, 18), (0, 18));
    AssertPolygon(upperLeftTrim.Polygons[1],
        (0, 18), (0, 88), (-18, 88), (-18, 18));

    var lowerRightTrim = BoardPlacementCalculator.CalculatePlanSideTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(100, -20),
        50,
        70,
        18);
    AssertPolygon(lowerRightTrim.Polygons[0],
        (0, 0), (50, 0), (50, -18), (0, -18));
    AssertPolygon(lowerRightTrim.Polygons[1],
        (0, -18), (0, -88), (18, -88), (18, -18));

    var lowerLeftTrim = BoardPlacementCalculator.CalculatePlanSideTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(-100, -20),
        50,
        70,
        18);
    AssertPolygon(lowerLeftTrim.Polygons[0],
        (0, 0), (-50, 0), (-50, -18), (0, -18));
    AssertPolygon(lowerLeftTrim.Polygons[1],
        (0, -18), (0, -88), (-18, -88), (-18, -18));

    var topUpperLeft = BoardPlacementCalculator.CalculatePlanTopTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(-100, 100),
        50,
        70,
        18);
    AssertPolygon(topUpperLeft.Polygons[0],
        (0, 0), (0, 50), (-18, 50), (-18, 0));
    AssertPolygon(topUpperLeft.Polygons[1],
        (-18, 0), (-88, 0), (-88, 18), (-18, 18));

    var topUpperRight = BoardPlacementCalculator.CalculatePlanTopTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(100, 100),
        50,
        70,
        18);
    AssertPolygon(topUpperRight.Polygons[0],
        (0, 0), (0, 50), (18, 50), (18, 0));
    AssertPolygon(topUpperRight.Polygons[1],
        (18, 0), (88, 0), (88, 18), (18, 18));

    var topLowerLeft = BoardPlacementCalculator.CalculatePlanTopTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(-100, -100),
        50,
        70,
        18);
    AssertPolygon(topLowerLeft.Polygons[0],
        (0, 0), (0, -50), (-18, -50), (-18, 0));
    AssertPolygon(topLowerLeft.Polygons[1],
        (-18, 0), (-88, 0), (-88, -18), (-18, -18));

    var topLowerRight = BoardPlacementCalculator.CalculatePlanTopTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(100, -100),
        50,
        70,
        18);
    AssertPolygon(topLowerRight.Polygons[0],
        (0, 0), (0, -50), (18, -50), (18, 0));
    AssertPolygon(topLowerRight.Polygons[1],
        (18, 0), (88, 0), (88, -18), (18, -18));

    Assert(!commands.Contains("candidate.FaceWidth", StringComparison.Ordinal) &&
           !commands.Contains("自动净距", StringComparison.Ordinal),
        "收口尺寸仍在使用柜体到墙的实际净距，而不是设置中的默认参数。" );

    var visiblePanel = BoardPlacementCalculator.CalculatePlanVisiblePanel(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(20, 0),
        new BoardPlacementPoint(0, 100),
        500,
        18);
    AssertPolygon(visiblePanel.Polygons[0],
        (0, 0), (18, 0), (18, 500), (0, 500));
}

static void BoardTrimSectionsSupportProfilesAndRotation()
{
    var anchor = new BoardPlacementPoint(0, 0);
    var lSection = BoardPlacementCalculator.CalculateTrimSection(
        anchor,
        BoardPlacementDirection.East,
        BoardPlacementDirection.South,
        50,
        70,
        18,
        BoardTrimSectionType.L);
    Assert(lSection.Polygons.Count == 2, "L型收口应生成正面板和端部辅助板。");
    AssertPolygon(lSection.Polygons[0],
        (0, 0), (50, 0), (50, -18), (0, -18));
    AssertPolygon(lSection.Polygons[1],
        (0, -18), (0, -88), (18, -88), (18, -18));
    AssertPolygonsDoNotOverlap(lSection.Polygons[0], lSection.Polygons[1]);

    var tSection = BoardPlacementCalculator.CalculateTrimSection(
        anchor,
        BoardPlacementDirection.East,
        BoardPlacementDirection.South,
        50,
        70,
        18,
        BoardTrimSectionType.T);
    Assert(tSection.Polygons.Count == 2, "T型收口应生成正面板和居中辅助板。");
    AssertPolygon(tSection.Polygons[1],
        (16, -18), (16, -88), (34, -88), (34, -18));
    AssertPolygonsDoNotOverlap(tSection.Polygons[0], tSection.Polygons[1]);

    ArgumentOutOfRangeException? narrowTException = null;
    try
    {
        BoardPlacementCalculator.CalculateTrimSection(
            anchor,
            BoardPlacementDirection.East,
            BoardPlacementDirection.South,
            17,
            70,
            18,
            BoardTrimSectionType.T);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        narrowTException = exception;
    }

    Assert(narrowTException is not null &&
           narrowTException.ParamName == "faceWidth" &&
           narrowTException.Message.Contains("T型收口正面宽度不能小于板厚", StringComparison.Ordinal),
        "A T profile narrower than its thickness should report a clear face-width error.");

    var iSection = BoardPlacementCalculator.CalculateTrimSection(
        anchor,
        BoardPlacementDirection.East,
        BoardPlacementDirection.South,
        50,
        70,
        18,
        BoardTrimSectionType.I);
    Assert(iSection.Polygons.Count == 1, "I型收口只能生成正面板。");
    AssertPolygon(iSection.Polygons[0],
        (0, 0), (50, 0), (50, -18), (0, -18));

    var rotatedTSections = new[]
    {
        BoardPlacementCalculator.CalculatePlanSideTrim(anchor, new BoardPlacementPoint(100, 100), 50, 70, 18, BoardTrimSectionType.T),
        BoardPlacementCalculator.CalculatePlanSideTrim(anchor, new BoardPlacementPoint(100, -100), 50, 70, 18, BoardTrimSectionType.T),
        BoardPlacementCalculator.CalculatePlanSideTrim(anchor, new BoardPlacementPoint(-100, -100), 50, 70, 18, BoardTrimSectionType.T),
        BoardPlacementCalculator.CalculatePlanSideTrim(anchor, new BoardPlacementPoint(-100, 100), 50, 70, 18, BoardTrimSectionType.T)
    };
    AssertPolygon(rotatedTSections[0].Polygons[1],
        (16, 18), (16, 88), (34, 88), (34, 18));
    AssertPolygon(rotatedTSections[1].Polygons[1],
        (16, -18), (16, -88), (34, -88), (34, -18));
    AssertPolygon(rotatedTSections[2].Polygons[1],
        (-16, -18), (-16, -88), (-34, -88), (-34, -18));
    AssertPolygon(rotatedTSections[3].Polygons[1],
        (-16, 18), (-16, 88), (-34, 88), (-34, 18));
    foreach (var layout in rotatedTSections)
    {
        AssertPolygonsDoNotOverlap(layout.Polygons[0], layout.Polygons[1]);
    }
}

static void TrimStartDetectorRecognizesLocalLines()
{
    AxisAlignedSegment[] leftSingle =
    [
        new(-100, 0, 0, 0)
    ];
    Assert(TrimStartDetector.TryDetect(
               new BoardPlacementPoint(0, 0),
               leftSingle,
               18,
               out var single),
        "起点左侧单线没有被识别。");
    AssertPoint(single.Anchor, 0, 0);
    Assert(single.LineSide == TrimStartLineSide.Left &&
           single.LineKind == TrimStartLineKind.Single,
        "起点左侧单线的方向或类型不正确。");

    AxisAlignedSegment[] leftDouble =
    [
        new(-100, 0, 0, 0),
        new(-100, -18, 0, -18),
        new(150, -100, 150, 100)
    ];
    Assert(TrimStartDetector.TryDetect(
               new BoardPlacementPoint(0, 0),
               leftDouble,
               18,
               out var leftPair),
        "起点左侧双线没有被识别。");
    AssertPoint(leftPair.Anchor, 0, 0);
    Assert(leftPair.LineSide == TrimStartLineSide.Left &&
           leftPair.LineKind == TrimStartLineKind.Double,
        "起点左侧双线的方向或类型不正确。");

    AxisAlignedSegment[] rightDouble =
    [
        new(0, 0, 100, 0),
        new(0, 18, 100, 18)
    ];
    Assert(TrimStartDetector.TryDetect(
               new BoardPlacementPoint(0, 0),
               rightDouble,
               18,
               out var rightPair),
        "起点右侧双线没有被识别。");
    AssertPoint(rightPair.Anchor, 0, 0);
    Assert(rightPair.LineSide == TrimStartLineSide.Right &&
           rightPair.LineKind == TrimStartLineKind.Double,
        "起点右侧双线的方向或类型不正确。");

    Assert(!TrimStartDetector.TryDetect(
            new BoardPlacementPoint(0, 0),
            [new AxisAlignedSegment(20, 0, 100, 0)],
            18,
            out _),
        "没有连接到起点的线不应参与收口定位。");
}

static void AssertPoint(BoardPlacementPoint point, double expectedX, double expectedY)
{
    Assert(Math.Abs(point.X - expectedX) < 0.000001 &&
           Math.Abs(point.Y - expectedY) < 0.000001,
        "识别出的定位角点不正确。");
}

static void AssertPolygonsDoNotOverlap(BoardPlacementPolygon first, BoardPlacementPolygon second)
{
    var firstMinX = first.Points.Min(point => point.X);
    var firstMaxX = first.Points.Max(point => point.X);
    var firstMinY = first.Points.Min(point => point.Y);
    var firstMaxY = first.Points.Max(point => point.Y);
    var secondMinX = second.Points.Min(point => point.X);
    var secondMaxX = second.Points.Max(point => point.X);
    var secondMinY = second.Points.Min(point => point.Y);
    var secondMaxY = second.Points.Max(point => point.Y);
    var overlapWidth = Math.Min(firstMaxX, secondMaxX) - Math.Max(firstMinX, secondMinX);
    var overlapHeight = Math.Min(firstMaxY, secondMaxY) - Math.Max(firstMinY, secondMinY);
    Assert(overlapWidth <= 0.000001 || overlapHeight <= 0.000001,
        "正面板与辅助板发生了面积重叠。");
}

static void AssertPolygon(BoardPlacementPolygon polygon, params (double X, double Y)[] expected)
{
    Assert(polygon.Points.Count == expected.Length, "板件轮廓顶点数量不正确。" );
    for (var index = 0; index < expected.Length; index++)
    {
        Assert(Math.Abs(polygon.Points[index].X - expected[index].X) < 0.000001 &&
               Math.Abs(polygon.Points[index].Y - expected[index].Y) < 0.000001,
            $"板件轮廓第 {index + 1} 个顶点不正确。" );
    }
}

static void CenterBoardAlignsToDetectedOpening()
{
    var bounds = new BoardSpaceBounds(100, 900, 200, 800);

    var horizontal = BoardPlacementCalculator.CalculateCenterBoard(
        bounds,
        new BoardPlacementPoint(800, 520),
        18);
    AssertPolygon(horizontal.Polygons.Single(),
        (100, 491), (900, 491), (900, 509), (100, 509));

    var vertical = BoardPlacementCalculator.CalculateCenterBoard(
        bounds,
        new BoardPlacementPoint(520, 750),
        18);
    AssertPolygon(vertical.Polygons.Single(),
        (491, 200), (509, 200), (509, 800), (491, 800));

    var forcedVertical = BoardPlacementCalculator.CalculateCenterBoard(
        bounds,
        new BoardPlacementPoint(800, 520),
        18,
        BoardCenterOrientation.Vertical);
    AssertPolygon(forcedVertical.Polygons.Single(),
        (491, 200), (509, 200), (509, 800), (491, 800));
}

static void CenterBoardOpeningUsesInnerEdgesOfDoubleLines()
{
    DoorOpeningSegment[] segments =
    [
        new(0, 0, 0, 718),
        new(18, 0, 18, 718),
        new(600, 0, 600, 718),
        new(618, 0, 618, 718),
        new(0, 0, 618, 0),
        new(0, 18, 618, 18),
        new(0, 700, 618, 700),
        new(0, 718, 618, 718)
    ];

    Assert(DoorOpeningDetector.TryDetect(300, 350, segments, out var bounds),
        "双线围成的柜体内空没有被识别。");
    Assert(bounds == new DoorPanelBounds(18, 600, 18, 700),
        $"中分板没有采用双线靠内空一侧的边界：{bounds}。");
}

static void OffsetBoardUsesClearSpacingOnFourSides()
{
    var bounds = new BoardSpaceBounds(0, 1000, 0, 800);

    var bottom = BoardPlacementCalculator.CalculateOffsetBoard(
        bounds,
        new BoardPlacementPoint(500, -100),
        100,
        18);
    Assert(bottom.Side == BoardOffsetSide.Bottom, "鼠标向下没有选择下侧净距。");
    AssertPolygon(bottom.Layout.Polygons.Single(),
        (0, 100), (1000, 100), (1000, 118), (0, 118));

    var top = BoardPlacementCalculator.CalculateOffsetBoard(
        bounds,
        new BoardPlacementPoint(500, 900),
        100,
        18);
    Assert(top.Side == BoardOffsetSide.Top, "鼠标向上没有选择上侧净距。");
    AssertPolygon(top.Layout.Polygons.Single(),
        (0, 682), (1000, 682), (1000, 700), (0, 700));

    var left = BoardPlacementCalculator.CalculateOffsetBoard(
        bounds,
        new BoardPlacementPoint(-100, 400),
        100,
        18);
    Assert(left.Side == BoardOffsetSide.Left, "鼠标向左没有选择左侧净距。");
    AssertPolygon(left.Layout.Polygons.Single(),
        (100, 0), (118, 0), (118, 800), (100, 800));

    var right = BoardPlacementCalculator.CalculateOffsetBoard(
        bounds,
        new BoardPlacementPoint(1100, 400),
        100,
        18);
    Assert(right.Side == BoardOffsetSide.Right, "鼠标向右没有选择右侧净距。");
    AssertPolygon(right.Layout.Polygons.Single(),
        (882, 0), (900, 0), (900, 800), (882, 800));

    AssertThrows<ArgumentOutOfRangeException>(
        () => BoardPlacementCalculator.CalculateOffsetBoard(
            bounds,
            new BoardPlacementPoint(500, -100),
            790,
            18),
        "位移净距与板厚超出内空时没有被拒绝。");
}

static void AbaBoardLayersKeepRearEdgesAligned()
{
    var layout = AbaBoardLayoutCalculator.Calculate(
        18, 500,
        9, 491,
        18, 500);

    Assert(layout.TotalThickness == 45 && layout.MaximumDepth == 500,
        "ABA夹心层板总厚或最大深度计算错误。");
    Assert(layout.Layers.Select(layer => layer.CenterOffset).SequenceEqual([13.5, 0, -13.5]),
        "第一、二、三层没有按绘制方向左侧到右侧排列。");
    Assert(layout.Layers.Select(layer => layer.FrontOffset).SequenceEqual([0d, 9d, 0d]),
        "第二层没有按三层深度差内退9毫米。");
    Assert(layout.Layers.All(layer => layer.BackOffset == 500),
        "三层夹心板的后端没有保持对齐。");

    var flush = AbaBoardLayoutCalculator.Calculate(9, 500, 18, 500, 9, 500);
    Assert(flush.Layers.All(layer => layer.FrontOffset == 0 && layer.BackOffset == 500),
        "默认零内退时三层没有前后齐平。");
    AssertThrows<ArgumentOutOfRangeException>(
        () => AbaBoardLayoutCalculator.Calculate(18, 500, 9, 0, 18, 500),
        "ABA夹心层板错误地接受了零深度。");

    var bounds = new BoardSpaceBounds(0, 1000, 0, 800);
    var bottom = AbaBoardLayoutCalculator.CalculateSpacePlacement(
        bounds, new BoardPlacementPoint(500, -100), 100, layout.TotalThickness);
    Assert(bottom.Side == BoardOffsetSide.Bottom, "ABA鼠标向下没有选择下侧空间内退。" );
    AssertPoint(bottom.Start, 1000, 122.5);
    AssertPoint(bottom.End, 0, 122.5);

    var top = AbaBoardLayoutCalculator.CalculateSpacePlacement(
        bounds, new BoardPlacementPoint(500, 900), 100, layout.TotalThickness);
    Assert(top.Side == BoardOffsetSide.Top, "ABA鼠标向上没有选择上侧空间内退。" );
    AssertPoint(top.Start, 0, 677.5);
    AssertPoint(top.End, 1000, 677.5);

    var left = AbaBoardLayoutCalculator.CalculateSpacePlacement(
        bounds, new BoardPlacementPoint(-100, 400), 100, layout.TotalThickness);
    Assert(left.Side == BoardOffsetSide.Left, "ABA鼠标向左没有选择左侧空间内退。" );
    AssertPoint(left.Start, 122.5, 0);
    AssertPoint(left.End, 122.5, 800);

    var right = AbaBoardLayoutCalculator.CalculateSpacePlacement(
        bounds, new BoardPlacementPoint(1100, 400), 100, layout.TotalThickness);
    Assert(right.Side == BoardOffsetSide.Right, "ABA鼠标向右没有选择右侧空间内退。" );
    AssertPoint(right.Start, 877.5, 800);
    AssertPoint(right.End, 877.5, 0);

    AssertThrows<ArgumentOutOfRangeException>(
        () => AbaBoardLayoutCalculator.CalculateSpacePlacement(
            bounds, new BoardPlacementPoint(500, -100), 780, layout.TotalThickness),
        "ABA空间内退与总厚超出内空时没有被拒绝。");
}

static void BackboardUsesRearDirectionAndIndependentExtensions()
{
    var bounds = new BoardSpaceBounds(0, 1000, 0, 800);
    var options = new BackboardLayoutOptions(
        Thickness: 8,
        Inset: 12,
        LeftExtension: 5,
        RightExtension: 6,
        TopExtension: 7,
        BottomExtension: 8);

    var top = BackboardLayoutCalculator.Calculate(
        bounds,
        new BoardPlacementPoint(500, 1000),
        options);
    Assert(top.RearSide == BoardOffsetSide.Top, "鼠标向上没有把上沿识别为背板后沿。" );
    AssertPolygon(top.PlanPolygon,
        (-5, 780), (1006, 780), (1006, 788), (-5, 788));
    AssertPolygon(top.ElevationPolygon,
        (-5, -8), (1006, -8), (1006, 807), (-5, 807));

    var bottom = BackboardLayoutCalculator.Calculate(
        bounds,
        new BoardPlacementPoint(500, -100),
        options);
    Assert(bottom.RearSide == BoardOffsetSide.Bottom, "鼠标向下没有把下沿识别为背板后沿。" );
    AssertPolygon(bottom.PlanPolygon,
        (-5, 12), (1006, 12), (1006, 20), (-5, 20));

    var left = BackboardLayoutCalculator.Calculate(
        bounds,
        new BoardPlacementPoint(-100, 400),
        options);
    Assert(left.RearSide == BoardOffsetSide.Left, "鼠标向左没有把左沿识别为背板后沿。" );
    AssertPolygon(left.PlanPolygon,
        (12, -8), (20, -8), (20, 807), (12, 807));

    var right = BackboardLayoutCalculator.Calculate(
        bounds,
        new BoardPlacementPoint(1100, 400),
        options);
    Assert(right.RearSide == BoardOffsetSide.Right, "鼠标向右没有把右沿识别为背板后沿。" );
    AssertPolygon(right.PlanPolygon,
        (980, -8), (988, -8), (988, 807), (980, 807));

    AssertThrows<ArgumentOutOfRangeException>(
        () => BackboardLayoutCalculator.Calculate(
            bounds,
            new BoardPlacementPoint(500, 1000),
            options with { Inset = 795 }),
        "背板缩进与厚度超过当前方向内空时没有被拒绝。" );

    var invalidModes = ToolboxSettings.CreateDefault();
    invalidModes.Board = invalidModes.Board with
    {
        BackboardGeneratePlan = true,
        BackboardGenerateElevation = true
    };
    AssertThrows<InvalidOperationException>(
        invalidModes.Validate,
        "背板俯视图和正视图没有被限制为严格二选一。" );
}

static void AdvancedBoardOperationsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "AdvancedBoardCommands.cs"));
    var statistics = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "StatisticsCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    string[] commandNames =
    [
        "DC_CMD_BOARD_DISPLACE", "DC_CMD_BOARD_ABA", "DC_CMD_BOARD_DIVIDE", "DC_CMD_BOARD_BACKBOARD",
        "DC_CMD_BOARD_SPLIT", "DC_CMD_BOARD_MERGE", "DC_CMD_BOARD_EXTEND", "DC_CMD_BOARD_TRIM",
        "DC_CMD_BOARD_SHELF_PROJECT"
    ];
    Assert(commandNames.All(name => commands.Contains(name, StringComparison.Ordinal)),
        "板件高级操作底层命令不完整。");
    Assert(commands.Contains("BoardDivisionCalculator.CalculatePlacement", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.CalculateSplitLayoutFromTwoPoints", StringComparison.Ordinal) &&
           commands.Contains("UseBasePoint = true", StringComparison.Ordinal) &&
           commands.Contains("TryGetSplitPoints", StringComparison.Ordinal) &&
           commands.Contains("foreach (var partBounds in layout.Parts)", StringComparison.Ordinal) &&
           commands.Contains("SplitBoardCore(document);", StringComparison.Ordinal) &&
           commands.Contains("分割操作未完成", StringComparison.Ordinal) &&
           !commands.Contains("SplitBoardPreviewJig", StringComparison.Ordinal) &&
           !commands.Contains("请选择分割方向 [竖向(V)/横向(H)]", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.CalculateNearestRectangleExtension", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.CalculateNearestLineTrim", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.CalculateNearestRectangleTrim", StringComparison.Ordinal) &&
           commands.Contains("CadAxisAlignedSegmentReader.CollectCurrentSpace", StringComparison.Ordinal) &&
           !commands.Contains("请选择边界直线", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.HaveCompatibleMergeProperties", StringComparison.Ordinal) &&
           commands.Contains("target.SetPropertiesFrom(source);", StringComparison.Ordinal) &&
           commands.Contains("target.XData = xdata;", StringComparison.Ordinal),
        "板件高级命令没有接入已验证的几何保护或属性保留。" );
    Assert(BoardToolOptions.Default.DisplaceDistance == 100 &&
           panel.Contains("x:Name=\"BoardDisplaceDistanceBox\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"内退距离 (mm)\"", StringComparison.Ordinal) &&
           panel.Contains("Foreground=\"White\" Content=\"位移板 (DCYC)\"", StringComparison.Ordinal) &&
           commands.Contains("TryGetBoardSpaceBounds", StringComparison.Ordinal) &&
           commands.Contains("PromptDoubleOptions", StringComparison.Ordinal) &&
           commands.Contains("CalculateOffsetBoard", StringComparison.Ordinal) &&
           commands.Contains("DisplaceBoardPreviewJig", StringComparison.Ordinal),
        "位移板没有使用主操作颜色，或未接入全局内退、单点内空识别、数值输入和四方向预览。" );
    Assert(BoardToolOptions.Default.EffectiveAbaFirstThickness == 18 &&
           BoardToolOptions.Default.EffectiveAbaSecondThickness == 9 &&
           BoardToolOptions.Default.EffectiveAbaThirdThickness == 18 &&
           BoardToolOptions.Default.EffectiveAbaFirstDepth == 500 &&
           BoardToolOptions.Default.EffectiveAbaSecondDepth == 500 &&
           BoardToolOptions.Default.EffectiveAbaThirdDepth == 500 &&
           BoardToolOptions.Default.AbaPlacementInset == 0 &&
           panel.Contains("Header=\"ABA夹心层板\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"AbaFirstThicknessBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"AbaSecondDepthBox\"", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"AbaPlacementInsetBox\"", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"AbaOverallInsetBox\"", StringComparison.Ordinal) &&
           commands.Contains("AbaBoardLayoutCalculator.Calculate", StringComparison.Ordinal) &&
           commands.Contains("TryGetBoardSpaceBounds", StringComparison.Ordinal) &&
           commands.Contains("请输入ABA夹心层板到所选内空边的空间内退", StringComparison.Ordinal) &&
           !commands.Contains("整体深度内退", StringComparison.Ordinal) &&
           !commands.Contains("depthInsetResult", StringComparison.Ordinal) &&
           commands.Contains("AbaBoardLayoutCalculator.CalculateSpacePlacement", StringComparison.Ordinal) &&
           commands.Contains("AbaBoardPreviewJig", StringComparison.Ordinal) &&
           commands.Contains("\"夹心层板\"", StringComparison.Ordinal),
        "ABA夹心层板仍包含整体深度内退，或没有接入识别后空间内退、鼠标四方向预览、三层独立深度和板件归类。" );
    Assert(BoardToolOptions.Default.DivideDepth == 100 &&
           BoardToolOptions.Default.DivideMaximumClearSpan == 430 &&
           BoardToolOptions.Default.DivideSmartMode &&
           BoardToolOptions.Default.DivideRemainderAtEnd &&
           panel.Contains("x:Name=\"BoardDivideDepthBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardDivideMaximumClearBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardDivideSmartRadio\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardDivideCountRadio\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BoardDivideRemainderCheck\"", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"BoardDivideCountBox\"", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"BoardDivideThicknessBox\"", StringComparison.Ordinal) &&
           commands.Contains("PromptIntegerOptions", StringComparison.Ordinal) &&
           commands.Contains("BoardRuntimeSettings.Thickness", StringComparison.Ordinal) &&
           commands.Contains("DivideBoardPreviewJig", StringComparison.Ordinal) &&
           !commands.Contains("AdvancedBoardRuntimeSettings.DivideThickness", StringComparison.Ordinal),
        "均分板件没有完整接入智能/按格数模式、全局板厚、独立深度、余量侧或鼠标预览。" );
    Assert(BoardToolOptions.Default.BackboardThickness == 9 &&
           BoardToolOptions.Default.BackboardGeneratePlan &&
           !BoardToolOptions.Default.BackboardGenerateElevation &&
           !BoardToolOptions.Default.BackboardHideElevationLayer &&
           panel.Contains("x:Name=\"BackboardGeneratePlanRadio\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BackboardGenerateElevationRadio\"", StringComparison.Ordinal) &&
           panel.Contains("GroupName=\"BackboardViewMode\"", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"BackboardGeneratePlanCheck\"", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"BackboardGenerateElevationCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"BackboardHideElevationCheck\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"点击内空生成背板 (DCBB)\"", StringComparison.Ordinal) &&
           commands.Contains("BackboardLayoutCalculator.Calculate", StringComparison.Ordinal) &&
           commands.Contains("TryGetBoardSpaceBounds", StringComparison.Ordinal) &&
           commands.Contains("BackboardPreviewJig", StringComparison.Ordinal) &&
           commands.Contains("TryEnsureDashedLinetype", StringComparison.Ordinal) &&
           commands.Contains("ColorMethod.ByAci, 8", StringComparison.Ordinal) &&
           commands.Contains("\"DASHED\"", StringComparison.Ordinal) &&
           commands.Contains("\"背板\",", StringComparison.Ordinal) &&
           commands.Contains("\"正视图背板\"", StringComparison.Ordinal) &&
           statistics.Contains("背板-俯视辅助图", StringComparison.Ordinal) &&
           statistics.Contains("or \"背板\"", StringComparison.Ordinal) &&
           statistics.Contains("ExpandWithHiddenBackboards", StringComparison.Ordinal) &&
           statistics.Contains("!layer.IsOff", StringComparison.Ordinal) &&
           statistics.Contains("IntersectsInPlan", StringComparison.Ordinal),
        "背板没有完整接入单点识别、鼠标后沿、俯视无属性、正视拆单或隐藏图层开关。" );
    Assert(panel.Contains("位移板", StringComparison.Ordinal) &&
           panel.Contains("生成夹心层板", StringComparison.Ordinal) &&
           panel.Contains("均分板件", StringComparison.Ordinal) &&
           panel.Contains("背板生成", StringComparison.Ordinal) &&
           panel.Contains("分割板件", StringComparison.Ordinal) &&
           !panel.Contains("拆分板件", StringComparison.Ordinal) &&
           panel.Contains("合并板件", StringComparison.Ordinal) &&
           panel.Contains("延伸矩形 (DCEXX)", StringComparison.Ordinal) &&
           !panel.Contains("就近延伸矩形", StringComparison.Ordinal) &&
           panel.Contains("修剪线/矩形", StringComparison.Ordinal) &&
           panel.Contains("生成层板侧视/立板俯视", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.TryMergeBounds", StringComparison.Ordinal) &&
           commands.Contains("HaveMatchingXData", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.CalculateNearestLineTrim", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.CalculateNearestRectangleTrim", StringComparison.Ordinal) &&
           commands.Contains("BoardGeometryCalculator.CalculateProjection", StringComparison.Ordinal) &&
           commands.Contains("BoardProjectionPreviewJig", StringComparison.Ordinal) &&
           commands.Contains("ApplyProjectionStyle", StringComparison.Ordinal) &&
           !commands.Contains("CloneSelection(document.Database, selection.Value", StringComparison.Ordinal),
        "板件高级操作界面入口不完整。");
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.AdvancedBoardCommands))", StringComparison.Ordinal),
        "板件高级命令类没有注册到插件。");
}

static void BoardLineReplacementOffsetsAreExact()
{
    Assert(BoardGeometryCalculator.GetParallelLineOffsets(2, 18).SequenceEqual([-9d, 9d]),
        "单线变双没有生成左右各半板厚的两条替换线。" );
    Assert(BoardGeometryCalculator.GetParallelLineOffsets(3, 18).SequenceEqual([-18d, 0d, 18d]),
        "单线变三没有生成左右边线和中心线。" );
    AssertThrows<ArgumentOutOfRangeException>(
        () => BoardGeometryCalculator.GetParallelLineOffsets(1, 18),
        "替换偏移错误地接受了非双线/三线数量。" );
}

static void BoardLineOffsetsAndExtensionsFollowGeometry()
{
    var bounds = new BoardSpaceBounds(0, 1000, 0, 800);
    var bottom = BoardPlacementCalculator.CalculateOffsetLines(
        bounds,
        new BoardPlacementPoint(500, -100),
        100,
        18,
        3);
    Assert(bottom.Side == BoardOffsetSide.Bottom &&
           bottom.Orientation == BoardCenterOrientation.Horizontal &&
           bottom.Coordinates.SequenceEqual([100d, 118d, 136d]),
        "三线没有从鼠标所指下边按净距、中心和板厚生成。" );

    var top = BoardPlacementCalculator.CalculateOffsetLines(
        bounds,
        new BoardPlacementPoint(500, 900),
        100,
        18,
        1);
    Assert(top.Side == BoardOffsetSide.Top && top.Coordinates.SequenceEqual([700d]),
        "单线没有从鼠标所指上边按输入距离生成。" );

    var right = BoardPlacementCalculator.CalculateOffsetLines(
        bounds,
        new BoardPlacementPoint(1100, 400),
        100,
        18,
        2);
    Assert(right.Side == BoardOffsetSide.Right &&
           right.Orientation == BoardCenterOrientation.Vertical &&
           right.Coordinates.SequenceEqual([882d, 900d]),
        "双线没有从鼠标所指右边按净距和板厚向内生成。" );

    var vertical = BoardGeometryCalculator.ExtendLineByDrawingAxes(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(0, 100),
        5,
        7);
    AssertPoint(vertical.Start, 0, -7);
    AssertPoint(vertical.End, 0, 105);

    var reversedVertical = BoardGeometryCalculator.ExtendLineByDrawingAxes(
        new BoardPlacementPoint(0, 100),
        new BoardPlacementPoint(0, 0),
        5,
        7);
    AssertPoint(reversedVertical.Start, 0, 105);
    AssertPoint(reversedVertical.End, 0, -7);

    var horizontal = BoardGeometryCalculator.ExtendLineByDrawingAxes(
        new BoardPlacementPoint(100, 0),
        new BoardPlacementPoint(0, 0),
        5,
        7);
    AssertPoint(horizontal.Start, 107, 0);
    AssertPoint(horizontal.End, -5, 0);
}

static void DoorPartitionGuidesUseSmartAndAdjustedCounts()
{
    var smart = BoardDivisionCalculator.CalculateDoorGuideLayout(
        3600,
        smartMode: true,
        maximumDoorWidth: 450,
        defaultDoorCount: 8);
    Assert(smart.DoorCount == 8 &&
           smart.BoundaryOffsets.SequenceEqual([0d, 450d, 900d, 1350d, 1800d, 2250d, 2700d, 3150d, 3600d]),
        "智能分区没有按最大门宽生成包含两端的门板边界线。" );

    var increased = BoardDivisionCalculator.CalculateDoorGuideLayout(
        3600,
        smartMode: true,
        maximumDoorWidth: 450,
        defaultDoorCount: 8,
        countAdjustment: 1);
    Assert(increased.DoorCount == 9 && increased.BoundaryOffsets.Count == 10 &&
           Math.Abs(increased.BoundaryOffsets[1] - 400) < 0.000001 &&
           Math.Abs(increased.BoundaryOffsets[^1] - 3600) < 0.000001,
        "W/+增加门扇数后没有重新严格等分。" );

    var fixedCount = BoardDivisionCalculator.CalculateDoorGuideLayout(
        3200,
        smartMode: false,
        maximumDoorWidth: 450,
        defaultDoorCount: 8,
        countAdjustment: -1);
    Assert(fixedCount.DoorCount == 7 && fixedCount.BoundaryOffsets.Count == 8 &&
           Math.Abs(fixedCount.BoundaryOffsets[^1] - 3200) < 0.000001,
        "按格数分使用S/-减少门扇数后没有保留起终点边界。" );
}

static void ShoeShelfLayoutFitsThinCabinetAndMirrorsFront()
{
    var options = new ShoeShelfLayoutOptions(
        18,
        260,
        true,
        20,
        200,
        0,
        0,
        20,
        0,
        20,
        true,
        0);
    var thinBounds = new BoardSpaceBounds(0, 200, 0, 1050);
    var right = ShoeShelfLayoutCalculator.Calculate(
        thinBounds,
        new BoardPlacementPoint(300, 525),
        options);

    Assert(right.FrontSide == ShoeCabinetFrontSide.Right && right.AngleDegrees > 45,
        "200mm超薄柜没有自动提高斜层板角度，或鼠标向右没有识别为右前方。" );
    Assert(right.Layers.Count >= 4 && right.Layers.All(layer => layer.Stopper is not null),
        "鞋柜斜层板没有按目标层距生成多层，或默认前挡条缺失。" );
    var firstShelf = right.Layers[0].Shelf;
    var firstStopper = right.Layers[0].Stopper!;
    var shelfLength = Math.Sqrt(
        Math.Pow(firstShelf.End.X - firstShelf.Start.X, 2) +
        Math.Pow(firstShelf.End.Y - firstShelf.Start.Y, 2));
    var shelfDirection = new BoardPlacementPoint(
        (firstShelf.End.X - firstShelf.Start.X) / shelfLength,
        (firstShelf.End.Y - firstShelf.Start.Y) / shelfLength);
    var shelfNormal = new BoardPlacementPoint(-shelfDirection.Y, shelfDirection.X);
    var shelfTopFront = firstShelf.Polygon.Points[1];
    var shelfBottomFront = firstShelf.Polygon.Points[2];
    var expectedStopperBackTop = new BoardPlacementPoint(
        shelfTopFront.X + shelfNormal.X * options.StopperHeight,
        shelfTopFront.Y + shelfNormal.Y * options.StopperHeight);
    var stopperLength = PointDistance(firstStopper.Start, firstStopper.End);
    Assert(PointDistance(firstStopper.Polygon.Points[0], shelfBottomFront) < 0.000001 &&
           PointDistance(firstStopper.Polygon.Points[1], expectedStopperBackTop) < 0.000001 &&
           Math.Abs(stopperLength - (options.Thickness + options.StopperHeight)) < 0.000001,
        "层板前端没有贴在挡条背面，或挡条总长没有包含板厚与外露高度。" );
    Assert(right.Layers.SelectMany(layer => layer.Shelf.Polygon.Points)
               .Concat(right.Layers.SelectMany(layer => layer.Stopper!.Polygon.Points))
               .All(point => point.X >= thinBounds.Left - 0.000001 &&
                             point.X <= thinBounds.Right + 0.000001 &&
                             point.Y >= thinBounds.Bottom - 0.000001 &&
                             point.Y <= thinBounds.Top + 0.000001),
        "智能增角后仍有层板或挡条超出识别内空。" );

    var standard = ShoeShelfLayoutCalculator.Calculate(
        new BoardSpaceBounds(0, 300, 0, 1050),
        new BoardPlacementPoint(400, 525),
        options);
    Assert(Math.Abs(standard.AngleDegrees - 20) < 0.000001,
        "标准柜深可以容纳20度层板时不应自动改变角度。" );

    var left = ShoeShelfLayoutCalculator.Calculate(
        thinBounds,
        new BoardPlacementPoint(-100, 525),
        options);
    Assert(left.FrontSide == ShoeCabinetFrontSide.Left && left.Layers.Count == right.Layers.Count,
        "鼠标向左没有镜像为左前方，或镜像后层数改变。" );
    Assert(Math.Abs(left.Layers[0].Shelf.Start.X - (thinBounds.Right - right.Layers[0].Shelf.Start.X)) < 0.000001 &&
           Math.Abs(left.Layers[0].Shelf.End.X - (thinBounds.Right - right.Layers[0].Shelf.End.X)) < 0.000001,
        "左右柜门方向没有围绕内空中心镜像。" );

    var increased = ShoeShelfLayoutCalculator.Calculate(
        thinBounds,
        new BoardPlacementPoint(300, 525),
        options with { CountAdjustment = 1 });
    Assert(increased.Layers.Count == right.Layers.Count + 1,
        "W/S或+/-对应的临时层数调整没有增加一层。" );

    var withoutStopper = ShoeShelfLayoutCalculator.Calculate(
        thinBounds,
        new BoardPlacementPoint(300, 525),
        options with { GenerateStopper = false });
    Assert(withoutStopper.Layers.All(layer => layer.Stopper is null),
        "关闭前挡条后仍然生成了挡条。" );

    AssertThrows<InvalidOperationException>(
        () => ShoeShelfLayoutCalculator.Calculate(
            thinBounds,
            new BoardPlacementPoint(300, 525),
            options with { SmartAngle = false }),
        "手动20度无法放入200mm超薄柜时应明确拒绝。" );

    var fixedAngleFallback = ShoeShelfLayoutCalculator.ResolvePreview(
        new BoardSpaceBounds(0, 260, 0, 1000),
        new BoardPlacementPoint(300, 500),
        options with { SmartAngle = false });
    Assert(fixedAngleFallback.UsedSmartFallback &&
           fixedAngleFallback.EffectiveOptions.SmartAngle &&
           fixedAngleFallback.Layout.Layers.Count > 0 &&
           fixedAngleFallback.Layout.AngleDegrees > options.PreferredAngleDegrees,
        "260mm内空中固定20度放不下时，没有在预览前安全回退到智能增角。" );

    static double PointDistance(BoardPlacementPoint first, BoardPlacementPoint second) =>
        Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
}

static void BoardDivisionSupportsSmartAndCountModes()
{
    var bounds = new BoardSpaceBounds(0, 1000, 0, 800);
    var smart = new BoardDivisionOptions(
        BoardDivisionMode.SmartMaximumClearSpan,
        18,
        LeaveRemainderAtEnd: true,
        MaximumClearSpan: 430);
    var right = BoardDivisionCalculator.CalculatePlacement(
        bounds,
        new BoardPlacementPoint(1200, 400),
        smart);
    Assert(right.IsVertical && right.RemainderSide == BoardOffsetSide.Right,
        "鼠标向右没有生成竖板或把余量留在右侧。" );
    Assert(right.Layout.CompartmentCount == 3 &&
           right.Layout.ClearSpans.SequenceEqual([321d, 321d, 322d]) &&
           right.DividerCoordinates.SequenceEqual([330d, 669d]),
        "最大净空430没有把1000内空智能分成321/321/322。" );

    var left = BoardDivisionCalculator.CalculatePlacement(
        bounds,
        new BoardPlacementPoint(-200, 400),
        smart);
    Assert(left.RemainderSide == BoardOffsetSide.Left &&
           left.DividerCoordinates.SequenceEqual([670d, 331d]),
        "鼠标向左没有镜像板位并把余量留在左侧。" );

    var up = BoardDivisionCalculator.CalculatePlacement(
        bounds,
        new BoardPlacementPoint(500, 1000),
        smart);
    Assert(!up.IsVertical && up.RemainderSide == BoardOffsetSide.Top &&
           up.Layout.ClearSpans.SequenceEqual([391d, 391d]) &&
           up.DividerCoordinates.SequenceEqual([400d]),
        "鼠标向上没有生成横板并把余量留在上侧。" );

    var byCount = new BoardDivisionOptions(
        BoardDivisionMode.CompartmentCount,
        18,
        LeaveRemainderAtEnd: true,
        CompartmentCount: 3);
    var manual = BoardDivisionCalculator.CalculateLayout(1000, byCount);
    Assert(manual.ClearSpans.SequenceEqual([321d, 321d, 322d]),
        "按3格分没有把1毫米取整余量留在最后一格。" );

    var strict = BoardDivisionCalculator.CalculateLayout(
        1000,
        byCount with { LeaveRemainderAtEnd = false });
    Assert(strict.ClearSpans.All(value => Math.Abs(value - 321.3333333333333) < 0.000001) &&
           Math.Abs(strict.DividerOffsets[0] - 330.3333333333333) < 0.000001 &&
           Math.Abs(strict.DividerOffsets[1] - 669.6666666666667) < 0.000001,
        "关闭余量留最后后没有保持严格等分。" );

    var small = BoardDivisionCalculator.CalculateLayout(400, smart);
    Assert(small.CompartmentCount == 1 && small.DividerOffsets.Count == 0,
        "智能分格在内空不超过430时不应添加板件。" );
    AssertThrows<ArgumentOutOfRangeException>(
        () => BoardDivisionCalculator.CalculateLayout(
            30,
            byCount with { CompartmentCount = 3 }),
        "均分板件没有拒绝板厚总和超过范围的输入。" );
}

static void BoardEditingGuardsRejectInvalidGeometry()
{
    Assert(BoardGeometryCalculator.IsInteriorSplitCoordinate(0, 100, 50) &&
           !BoardGeometryCalculator.IsInteriorSplitCoordinate(0, 100, -10) &&
           !BoardGeometryCalculator.IsInteriorSplitCoordinate(0, 100, 100),
        "拆分位置没有严格限制在板件内部。" );
    AssertThrows<InvalidOperationException>(
        () => BoardGeometryCalculator.CalculateLineTrim(-0.5, 0.25),
        "修剪线错误地接受了原线段外部的交点。" );
    AssertThrows<InvalidOperationException>(
        () => BoardGeometryCalculator.CalculateLineTrim(0.5, 0.5),
        "修剪线错误地接受了交点本身作为待删除段。" );
}

static void BoardSplitUsesTwoPositionsAlongLongAxis()
{
    var horizontalBoard = BoardGeometryCalculator.CalculateSplitLayoutFromTwoPoints(
        new BoardSpaceBounds(0, 1000, 0, 100),
        new BoardPlacementPoint(300, 0),
        new BoardPlacementPoint(700, 100));
    Assert(horizontalBoard.Orientation == BoardSplitOrientation.Vertical &&
           horizontalBoard.Coordinates.SequenceEqual([300d, 700d]) &&
           horizontalBoard.Parts.SequenceEqual(
           [
               new BoardSpaceBounds(0, 300, 0, 100),
               new BoardSpaceBounds(300, 700, 0, 100),
               new BoardSpaceBounds(700, 1000, 0, 100)
           ]),
        "横向板件没有按两个X位置分成左、中、右三个独立矩形布局。" );

    var verticalBoard = BoardGeometryCalculator.CalculateSplitLayoutFromTwoPoints(
        new BoardSpaceBounds(0, 100, 0, 1000),
        new BoardPlacementPoint(0, 750),
        new BoardPlacementPoint(100, 250));
    Assert(verticalBoard.Orientation == BoardSplitOrientation.Horizontal &&
           verticalBoard.Coordinates.SequenceEqual([250d, 750d]) &&
           verticalBoard.Parts.SequenceEqual(
           [
               new BoardSpaceBounds(0, 100, 0, 250),
               new BoardSpaceBounds(0, 100, 250, 750),
               new BoardSpaceBounds(0, 100, 750, 1000)
           ]),
        "竖向板件没有自动排序两个Y位置并分成下、中、上三个独立矩形布局。" );

    var samePosition = BoardGeometryCalculator.CalculateSplitLayoutFromTwoPoints(
        new BoardSpaceBounds(0, 1000, 0, 100),
        new BoardPlacementPoint(500, 0),
        new BoardPlacementPoint(500, 100));
    Assert(samePosition.Coordinates.SequenceEqual([500d]) &&
           samePosition.Parts.SequenceEqual(
           [
               new BoardSpaceBounds(0, 500, 0, 100),
               new BoardSpaceBounds(500, 1000, 0, 100)
           ]),
        "同一分割位置没有按一条切割线生成两个独立矩形布局。" );

    AssertThrows<ArgumentOutOfRangeException>(
        () => BoardGeometryCalculator.CalculateSplitLayoutFromTwoPoints(
            new BoardSpaceBounds(0, 1000, 0, 100),
            new BoardPlacementPoint(-10, 200),
            new BoardPlacementPoint(700, 100)),
        "分割错误地接受了矩形范围外的点。" );
}

static void BoardMergePropertiesMustMatch()
{
    Assert(BoardGeometryCalculator.TryMergeBounds(
               new BoardSpaceBounds(0, 100, 0, 50),
               new BoardSpaceBounds(100, 200, 0, 50),
               out var horizontal) &&
           horizontal == new BoardSpaceBounds(0, 200, 0, 50),
        "左右完整相邻的板件没有合并为正确外框。" );
    Assert(BoardGeometryCalculator.TryMergeBounds(
               new BoardSpaceBounds(0, 100, 0, 50),
               new BoardSpaceBounds(0, 100, 50, 120),
               out var vertical) &&
           vertical == new BoardSpaceBounds(0, 100, 0, 120),
        "上下完整相邻的板件没有合并为正确外框。" );
    Assert(!BoardGeometryCalculator.TryMergeBounds(
               new BoardSpaceBounds(0, 100, 0, 50),
               new BoardSpaceBounds(90, 200, 0, 50),
               out _) &&
           !BoardGeometryCalculator.TryMergeBounds(
               new BoardSpaceBounds(0, 100, 0, 50),
               new BoardSpaceBounds(110, 200, 0, 50),
               out _) &&
           !BoardGeometryCalculator.TryMergeBounds(
               new BoardSpaceBounds(0, 100, 0, 50),
               new BoardSpaceBounds(100, 200, 5, 45),
               out _),
        "合并错误地接受了重叠、有间隙或边缘未完整对齐的板件。" );
    Assert(BoardGeometryCalculator.HaveCompatibleMergeProperties("0-柜体结构", "0-柜体结构", -500, -500, 0, 0),
        "相同属性的相邻板件被错误拒绝合并。" );
    Assert(!BoardGeometryCalculator.HaveCompatibleMergeProperties("0-柜体结构", "0-背板", -500, -500, 0, 0) &&
           !BoardGeometryCalculator.HaveCompatibleMergeProperties("0-柜体结构", "0-柜体结构", -500, -450, 0, 0) &&
           !BoardGeometryCalculator.HaveCompatibleMergeProperties("0-柜体结构", "0-柜体结构", -500, -500, 0, 18),
        "不同图层、深度或高程的板件仍被允许合并。" );
}

static void BoardEditingUsesPickedSide()
{
    var clickedRightOfEarlyIntersection = BoardGeometryCalculator.CalculateLineTrim(0.2, 0.3);
    var clickedLeftOfLateIntersection = BoardGeometryCalculator.CalculateLineTrim(0.8, 0.7);
    Assert(clickedRightOfEarlyIntersection == new BoardLineEditParameters(0, 0.2) &&
           clickedLeftOfLateIntersection == new BoardLineEditParameters(0.8, 1),
        "修剪线没有删除实际点取的线段，而是按最近端点删除了相反方向。" );
    AssertThrows<InvalidOperationException>(
        () => BoardGeometryCalculator.CalculateLineTrim(0.5, 0.5),
        "修剪线错误地接受了无法判断删除侧的交点点取。" );

    var bounds = new BoardSpaceBounds(0, 100, 0, 50);
    Assert(BoardGeometryCalculator.CalculateRectangleEdit(
               bounds,
               BoardRectangleSide.Left,
               -20,
               isExtend: true) == new BoardSpaceBounds(-20, 100, 0, 50),
        "矩形左边没有正确延伸到外侧边界。" );
    Assert(BoardGeometryCalculator.CalculateRectangleEdit(
               bounds,
               BoardRectangleSide.Left,
               20,
               isExtend: false) == new BoardSpaceBounds(20, 100, 0, 50),
        "矩形修剪没有删除点取的左侧部分。" );
    AssertThrows<InvalidOperationException>(
        () => BoardGeometryCalculator.CalculateRectangleEdit(
            bounds,
            BoardRectangleSide.Right,
            80,
            isExtend: true),
        "矩形延伸错误地接受了内部边界。" );
}

static void BoardNearestTrimFindsClickedSideBoundary()
{
    AxisAlignedSegment[] lineBoundaries =
    [
        new(60, -10, 60, 10),
        new(80, -10, 80, 10),
        new(85, 10, 85, 20),
        new(95, -10, 95, 10),
        new(0, 20, 100, 20)
    ];
    var trimRight = BoardGeometryCalculator.CalculateNearestLineTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(100, 0),
        new BoardPlacementPoint(90, 0),
        lineBoundaries);
    Assert(trimRight == new BoardLineEditParameters(0, 0.8),
        "一键修剪没有忽略点击点外侧或不相交的线并选择右端内侧最近交线。" );

    AxisAlignedSegment[] leftBoundaries =
    [
        new(20, -10, 20, 10),
        new(40, -10, 40, 10),
        new(5, -10, 5, 10)
    ];
    var trimLeft = BoardGeometryCalculator.CalculateNearestLineTrim(
        new BoardPlacementPoint(0, 0),
        new BoardPlacementPoint(100, 0),
        new BoardPlacementPoint(10, 0),
        leftBoundaries);
    Assert(trimLeft == new BoardLineEditParameters(0.2, 1),
        "一键修剪没有选择左端点击点内侧最近交线。" );

    var horizontalBounds = new BoardSpaceBounds(0, 100, 0, 20);
    AxisAlignedSegment[] rectangleBoundaries =
    [
        new(60, 0, 60, 20),
        new(80, 5, 80, 15),
        new(85, 30, 85, 40),
        new(95, 0, 95, 20)
    ];
    var rectangle = BoardGeometryCalculator.CalculateNearestRectangleTrim(
        horizontalBounds,
        new BoardPlacementPoint(90, 20),
        rectangleBoundaries);
    Assert(rectangle.Side == BoardRectangleSide.Right &&
           rectangle.BoundaryCoordinate == 80 &&
           rectangle.Bounds == new BoardSpaceBounds(0, 80, 0, 20),
        "矩形一键修剪没有只缩短点击端，或错误改变了板件厚度。" );

    AssertThrows<InvalidOperationException>(
        () => BoardGeometryCalculator.CalculateNearestLineTrim(
            new BoardPlacementPoint(0, 0),
            new BoardPlacementPoint(100, 0),
            new BoardPlacementPoint(50, 0),
            lineBoundaries),
        "一键修剪错误地接受了无法判断端部方向的中心点击。" );
}

static void BoardEditingHandlesCornerPickAndClipboardContention()
{
    var bounds = new BoardSpaceBounds(0, 100, 0, 50);
    var corner = new BoardPlacementPoint(0, 0);
    Assert(BoardGeometryCalculator.SelectRectangleEditSide(bounds, corner, verticalBoundary: false) ==
           BoardRectangleSide.Bottom,
        "水平边界下点取矩形角点时没有选择下边，仍会被误判为左边。" );
    Assert(BoardGeometryCalculator.SelectRectangleEditSide(bounds, corner, verticalBoundary: true) ==
           BoardRectangleSide.Left,
        "竖直边界下点取矩形角点时没有选择左边。" );

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "AdvancedBoardCommands.cs"));
    Assert(commands.Contains("ClipboardBusyErrorCode", StringComparison.Ordinal) &&
           commands.Contains("catch (ExternalException exception) when (exception.ErrorCode == ClipboardBusyErrorCode)", StringComparison.Ordinal),
        "延伸/修剪点选没有隔离 0x800401D0 瞬时剪贴板占用异常。" );
}

static void BoardExtensionFindsNearestValidBoundary()
{
    var horizontalBounds = new BoardSpaceBounds(0, 100, 0, 20);
    AxisAlignedSegment[] segments =
    [
        new(120, 30, 120, 50),
        new(150, 5, 150, 15),
        new(180, 0, 180, 20),
        new(-30, 0, -30, 20),
        new(0, 30, 100, 30),
        new(0, -40, 100, -40)
    ];

    var right = BoardGeometryCalculator.CalculateNearestRectangleExtension(
        horizontalBounds,
        new BoardPlacementPoint(90, 20),
        segments);
    Assert(right.Side == BoardRectangleSide.Right &&
           right.BoundaryCoordinate == 150 &&
           right.Bounds == new BoardSpaceBounds(0, 150, 0, 20),
        "横向板点击靠右的上边后没有只延伸右端长度，或错误改变了厚度。" );

    var left = BoardGeometryCalculator.CalculateNearestRectangleExtension(
        horizontalBounds,
        new BoardPlacementPoint(10, 10),
        segments);
    Assert(left.Side == BoardRectangleSide.Left &&
           left.Bounds == new BoardSpaceBounds(-30, 100, 0, 20),
        "横向板点击靠左位置后没有只延伸左端长度。" );

    var verticalBounds = new BoardSpaceBounds(0, 20, 0, 100);
    AxisAlignedSegment[] verticalSegments =
    [
        new(5, 150, 15, 150),
        new(0, 180, 20, 180),
        new(30, 0, 30, 100)
    ];
    var top = BoardGeometryCalculator.CalculateNearestRectangleExtension(
        verticalBounds,
        new BoardPlacementPoint(20, 90),
        verticalSegments);
    Assert(top.Side == BoardRectangleSide.Top &&
           top.Bounds == new BoardSpaceBounds(0, 20, 0, 150),
        "竖向板点击靠上位置后没有只延伸上端长度，或错误改变了宽度。" );

    AssertThrows<InvalidOperationException>(
        () => BoardGeometryCalculator.CalculateNearestRectangleExtension(
            horizontalBounds,
            new BoardPlacementPoint(50, 10),
            segments),
        "延伸错误地接受了无法判断长边端部方向的中心点。" );
}

static void BoardProjectionUsesReferenceOrientationAndDepthRange()
{
    var shelf = BoardGeometryCalculator.CalculateProjection(
        new BoardSpaceBounds(100, 1100, 500, 518),
        new BoardPlacementPoint(1300, 0),
        new BoardPlacementPoint(1800, 20));
    Assert(shelf.Kind == BoardProjectionKind.ShelfSideView &&
           shelf.Bounds == new BoardSpaceBounds(1300, 1800, 500, 518),
        "横向层板没有保留高度和板厚生成侧视深度投影。" );

    var verticalBoard = BoardGeometryCalculator.CalculateProjection(
        new BoardSpaceBounds(400, 418, 0, 2000),
        new BoardPlacementPoint(0, -600),
        new BoardPlacementPoint(20, -100));
    Assert(verticalBoard.Kind == BoardProjectionKind.VerticalBoardPlanView &&
           verticalBoard.Bounds == new BoardSpaceBounds(400, 418, -600, -100),
        "竖向立板没有保留左右位置和板厚生成俯视深度投影。" );

    AssertThrows<ArgumentOutOfRangeException>(
        () => BoardGeometryCalculator.CalculateProjection(
            new BoardSpaceBounds(100, 1100, 500, 518),
            new BoardPlacementPoint(1300, 0),
            new BoardPlacementPoint(1300, 500)),
        "层板投影错误地接受了没有横向深度的两点范围。" );
}

static void StatisticsModuleSurfaceAndCommandsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "StatisticsCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    Assert(panel.Contains("x:Name=\"StatisticsModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"部件统计\"", StringComparison.Ordinal) &&
           !panel.Contains("Header=\"全屋拆单\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"普通门板\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"边框门板\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"柜体拆单\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"板件统计\"", StringComparison.Ordinal),
        "拆单统计菜单或五个页签不完整。");
    Assert(commands.Contains("DC_CMD_PARTS_STATISTICS", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_DOOR_STATISTICS", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_FRAME_DOOR_STATISTICS", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_CABINET_BOM", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_BOARD_STATISTICS", StringComparison.Ordinal),
        "拆单统计底层命令不完整。");
    Assert(commands.Contains("Compatibility.Contains(polyline.Layer, \"柜体结构\"", StringComparison.Ordinal) &&
           commands.Contains("var boardThickness = Math.Min(width, height)", StringComparison.Ordinal) &&
           !commands.Contains("combined.Contains(\"板\"", StringComparison.Ordinal),
        "柜体板件没有按长度×深度×板厚换算，或普通门板仍会混入柜体拆单。");
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.StatisticsCommands))", StringComparison.Ordinal),
        "拆单统计命令类没有注册到插件。");
}

static void AdvancedStatisticsConfigurationAndBridgeAreComplete()
{
    var door = DoorStatisticsOptions.Default;
    var frame = FrameDoorStatisticsOptions.Default;
    var cabinet = CabinetBomOptions.Default;
    var nesting = PanelNestingOptions.Default;
    Assert(door.TableScaleFactor == 8 && door.Thickness == 18 && door.MaxRowsPerPage == 30,
        "普通门板统计默认参数不完整。" );
    Assert(frame.CabinetThickness == 18 && frame.BoardCoreCount == 2 && frame.GlassCoreCount == 0 &&
           frame.CoreThickness == 12 && frame.SideFrameWidth == 60 && frame.TopFrameWidth == 60 &&
           frame.MiddleFrameWidth == 60 && frame.BottomFrameWidth == 60 &&
           frame.RightFrameTenon == 10 && frame.MiddleFrameTenon == 10 &&
           frame.CoreTopTenon == 8 && frame.CoreBottomTenon == 8 &&
           frame.CoreLeftTenon == 8 && frame.CoreRightTenon == 8 &&
           frame.CenterMiddleFrame && frame.LowerFramePosition == 600 &&
           frame.TableScaleFactor == 8 && frame.MaxRowsPerPage == 30,
        "边框门板统计默认参数与已确认工艺不一致。" );
    Assert(cabinet.IncludeBasicBoards && cabinet.IncludeBackboards && cabinet.IncludeCabinetSummary &&
           cabinet.TableScaleFactor == 8 && cabinet.MaxRowsPerPage == 30,
        "柜体拆单默认项目或表格参数不完整。" );
    Assert(nesting.UseWoodGrain && !nesting.UseSolidColor && nesting.Use48Sheet && !nesting.Use49Sheet &&
           nesting.IncludeCabinetBoards && nesting.IncludeFlatDoors &&
           nesting.TableScaleFactor == 8 && nesting.MaxRowsPerPage == 30,
        "板件统计默认材质、开料板或统计对象不正确。" );

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "StatisticsCommands.cs"));
    var bridge = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "LeishenBomBridge.cs"));
    Assert(panel.Contains("x:Name=\"DoorStatisticsThicknessBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"FrameDoorCenterMiddleFrameCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"CabinetBomBasicBoardsCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"PanelNestingWoodGrainRadio\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"外观图超级复制 (DCCOO)\"", StringComparison.Ordinal),
        "五类拆单统计高级参数或外观图超级复制入口不完整。" );
    Assert(bridge.Contains("LS.Module.Bom.Models.BomParams", StringComparison.Ordinal) &&
           bridge.Contains("LS.Module.Bom.Models.DoorStatCmdsParams", StringComparison.Ordinal) &&
           bridge.Contains("LS.Module.Bom.Models.FrameDoorParams", StringComparison.Ordinal) &&
           bridge.Contains("LS.Module.Bom.Models.CabinetBomParams", StringComparison.Ordinal) &&
           bridge.Contains("LS.Module.Bom.Models.PanelNestingParams", StringComparison.Ordinal) &&
           bridge.Contains("[\"I_TopTenon\"]", StringComparison.Ordinal) &&
           bridge.Contains("[\"J_BottomTenon\"]", StringComparison.Ordinal) &&
           bridge.Contains("[\"K_LeftTenon\"]", StringComparison.Ordinal) &&
           bridge.Contains("[\"L_RightTenon\"]", StringComparison.Ordinal) &&
           bridge.Contains("[\"Is48Panel\"]", StringComparison.Ordinal) &&
           bridge.Contains("[\"Is49Panel\"]", StringComparison.Ordinal) &&
           bridge.Contains("[\"IsSolidColor\"]", StringComparison.Ordinal) &&
           bridge.Contains("LS_CMD_BOM_ONECLICK", StringComparison.Ordinal),
        "雷神 BOM 公共参数模型或一键拆单命令桥接不完整。" );
    Assert(commands.Contains("DC_CMD_BOM_VISUAL_COPY", StringComparison.Ordinal) &&
           commands.Contains("LeishenBomBridge.TryQueueCommand", StringComparison.Ordinal) &&
           commands.Contains("LeishenBomBridge.VisualCopyCommand", StringComparison.Ordinal) &&
           bridge.Contains("LS_CMD_BOM_VISUALCOPY", StringComparison.Ordinal),
        "拆单统计命令没有桥接成熟模块，或缺少外观图超级复制命令。" );
}

static void DrawingStatisticsAggregateAndExport()
{
    var rows = DrawingStatistics.Aggregate(
    [
        new DrawingPart("板件", "层板", 600, 500, 18),
        new DrawingPart("板件", "层板", 600.0001, 500, 18),
        new DrawingPart("门板", "平开门", 400, 700, 18)
    ]);

    Assert(rows.Count == 2 && rows.Single(row => row.Name == "层板").Quantity == 2,
        "相同规格没有按毫米精度汇总。");
    var path = Path.Combine(Path.GetTempPath(), $"dc-statistics-{Guid.NewGuid():N}.csv");
    try
    {
        DrawingStatistics.ExportCsv(path, rows);
        var bytes = File.ReadAllBytes(path);
        var text = File.ReadAllText(path, Encoding.UTF8);
        Assert(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF &&
               text.Contains("类别,名称,宽度(mm),高度(mm),厚度(mm),数量,面积(m²)", StringComparison.Ordinal),
            "统计 CSV 不是 UTF-8 BOM 或缺少标准表头。");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void WholeHouseStatisticsSupportsCadTableAndExcel()
{
    var rows = DrawingStatistics.Aggregate(
    [
        new DrawingPart("柜体板件", "层板", 600, 500, 18, 2),
        new DrawingPart("普通门板", "平开门", 400, 700, 18)
    ]);
    var path = Path.Combine(Path.GetTempPath(), $"Drawing1_柜拆单-{Guid.NewGuid():N}.xls");
    try
    {
        DrawingStatistics.ExportExcelXml(path, "柜拆单", rows);
        var document = XDocument.Load(path);
        XNamespace spreadsheet = "urn:schemas-microsoft-com:office:spreadsheet";
        Assert(document.Descendants(spreadsheet + "Worksheet")
                   .Any(element => string.Equals((string?)element.Attribute(spreadsheet + "Name"), "柜拆单", StringComparison.Ordinal)) &&
               document.Descendants(spreadsheet + "Data").Any(element => element.Value == "层板") &&
               document.Descendants(spreadsheet + "Data").Any(element => element.Value == "2"),
            "Excel 97-2003 XML 总表缺少工作表、板件名称或数量。");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "StatisticsCommands.cs"));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    Assert(commands.Contains("DC_CMD_WHOLE_HOUSE_BOM", StringComparison.Ordinal) &&
           commands.Contains("请框选参与一键全屋拆单的对象", StringComparison.Ordinal) &&
           commands.Contains("GetSelection(selectionOptions)", StringComparison.Ordinal) &&
           !commands.Contains("CollectAll(document.Database, StatisticsMode.WholeHouse)", StringComparison.Ordinal) &&
           commands.Contains("Keywords.Add(\"Table\", \"T\", \"放置CAD表格(T)\")", StringComparison.Ordinal) &&
           commands.Contains("Keywords.Add(\"Excel\", \"E\", \"导出Excel总表(E)\")", StringComparison.Ordinal) &&
           commands.Contains("GetXDataForApplication(BoardCommands.BoardPropertyAppName)", StringComparison.Ordinal) &&
           commands.Contains("category = boardProperty?.Category ?? category", StringComparison.Ordinal) &&
           commands.Contains("MaxRowsPerPage", StringComparison.Ordinal) &&
           commands.Contains("TableScaleFactor", StringComparison.Ordinal) &&
           commands.Contains("new Table", StringComparison.Ordinal) &&
           commands.Contains("Drawing1_柜拆单.xls", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"WholeHouseCabinetBoardsCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"WholeHouseBoardPropertiesCheck\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"WholeHouseTableScaleBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"WholeHouseMaxRowsBox\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"一键全屋拆单 (DCQW)\"", StringComparison.Ordinal),
        "一键全屋拆单未强制先框选，或缺少项目配置、表格参数与 T/E 双输出。");
}

static void FrameAlignmentMovesContainedGeometry()
{
    var frames = new[]
    {
        new PrintFrame(0, 0, 100, 80, "A"),
        new PrintFrame(200, 20, 320, 100, "B")
    };
    var moves = FrameAlignmentCalculator.ArrangeFromLeft(frames, 1000, 50);

    Assert(moves.Count == 2 &&
           moves[0].Source.SourceKey == "A" && moves[0].DeltaX == 1000 && moves[0].DeltaY == 0 &&
           moves[1].Source.SourceKey == "B" && moves[1].DeltaX == 950 && moves[1].DeltaY == -20,
        "图框左对齐没有按基线、间距和共同底边生成稳定移动量。");

    var inside = FrameAlignmentCalculator.FindContentMove(
        new PrintFrame(220, 30, 250, 60),
        moves);
    var outside = FrameAlignmentCalculator.FindContentMove(
        new PrintFrame(500, 30, 520, 60),
        moves);
    Assert(inside is { Source.SourceKey: "B" } && outside is null,
        "框内图形没有归属到对应图框，或框外图形被错误带动。");
}

static void RadialMenuDefaultsAndHitTestingAreStable()
{
    var options = RadialMenuOptions.CreateDefault();
    options.Validate();
    Assert(options.Enabled && options.AutoCloseAfterCommand && options.ShowThirdRing &&
           options.Radius == 200 && Math.Abs(options.Opacity - 1) < 0.000001 &&
           options.Skin == RadialMenuOptions.DefaultSkin,
        "圆盘默认开关、大小、透明度或橙灰皮肤与确认界面不一致。");
    Assert(RadialMenuOptions.IsSupportedSkin(RadialMenuOptions.DefaultSkin) &&
           RadialMenuOptions.IsSupportedSkin(RadialMenuOptions.CadDarkSkin) &&
           RadialMenuOptions.IsSupportedSkin(RadialMenuOptions.PurpleGraySkin),
        "圆盘没有完整声明橙灰、CAD深色和紫灰三套受支持皮肤。");
    Assert(options.Items.Count(item => item.Ring == 1) == 8 &&
           options.Items.Count(item => item.Ring == 2) == 16 &&
           options.Items.Count(item => item.Ring == 3) == 24,
        "圆盘默认配置不是8/16/24三个完整圆环。");
    Assert(options.Items.Single(item => item.Ring == 1 && item.Position == 1) is { Name: "", CommandAlias: "" } &&
           options.Items.Single(item => item.Ring == 1 && item.Position == 7).CommandAlias == "DCMB" &&
           options.Items.Single(item => item.Ring == 2 && item.Position == 3) is { Name: "", CommandAlias: "" } &&
           options.Items.Single(item => item.Ring == 2 && item.Position == 14).CommandAlias == "DCDY",
        "圆盘仍预填项目未实现的命令，或已实现命令没有保留。" );

    var topInner = RadialMenuHitTester.HitTest(0, -60, 200, true);
    var topMiddle = RadialMenuHitTester.HitTest(0, -120, 200, true);
    var topOuter = RadialMenuHitTester.HitTest(0, -180, 200, true);
    Assert(topInner == new RadialMenuSlot(1, 1) &&
           topMiddle == new RadialMenuSlot(2, 1) &&
           topOuter == new RadialMenuSlot(3, 1),
        "圆盘顶部没有稳定命中每圈第一个位置。");
    Assert(RadialMenuHitTester.HitTest(0, 0, 200, true) is null &&
           RadialMenuHitTester.HitTest(0, -180, 200, false) is null,
        "圆盘中心关闭区或隐藏第三圈仍被识别为命令。");
}

static void RadialMenuOptionsPersistAndRejectInvalidValues()
{
    var path = Path.Combine(Path.GetTempPath(), $"dc-radial-{Guid.NewGuid():N}.json");
    try
    {
        var options = RadialMenuOptions.CreateDefault();
        options.Radius = 220;
        options.Opacity = 0.8;
        var configuredSlot = options.Items.Single(item => item.Ring == 1 && item.Position == 1);
        configuredSlot.Name = "生成门板";
        configuredSlot.CommandAlias = "DCMB";
        RadialMenuOptionsStore.Save(path, options);
        var loaded = RadialMenuOptionsStore.Load(path);
        Assert(loaded.Radius == 220 && Math.Abs(loaded.Opacity - 0.8) < 0.000001 &&
               loaded.Items.Single(item => item.Ring == 1 && item.Position == 1).CommandAlias == "DCMB",
            "圆盘大小、透明度或命令配置没有持久化。");

        var legacyBatchPrintSlot = options.Items.Single(item => item.Ring == 2 && item.Position == 14);
        legacyBatchPrintSlot.CommandAlias = "DY";
        RadialMenuOptionsStore.Save(path, options);
        loaded = RadialMenuOptionsStore.Load(path);
        Assert(loaded.Items.Single(item => item.Ring == 2 && item.Position == 14).CommandAlias == "DCDY",
            "圆盘旧版批量打印默认命令没有迁移为 DC 前缀。" );

        options.Items.Single(item => item.Ring == 2 && item.Position == 1).CommandAlias = "DCZ2";
        options.Items.Single(item => item.Ring == 2 && item.Position == 2).CommandAlias = "DCZ3";
        RadialMenuOptionsStore.Save(path, options);
        loaded = RadialMenuOptionsStore.Load(path);
        Assert(loaded.Items.Single(item => item.Ring == 2 && item.Position == 1).CommandAlias == "DC22" &&
               loaded.Items.Single(item => item.Ring == 2 && item.Position == 2).CommandAlias == "DC33",
            "圆盘旧单双线命令没有迁移为 DC22/DC33。" );

        options.Skin = "科技深蓝 (Tech Blue)";
        RadialMenuOptionsStore.Save(path, options);
        loaded = RadialMenuOptionsStore.Load(path);
        Assert(loaded.Skin == RadialMenuOptions.DefaultSkin,
            "圆盘旧科技深蓝皮肤没有迁移为全局橙灰皮肤。" );

        options.Skin = RadialMenuOptions.CadDarkSkin;
        RadialMenuOptionsStore.Save(path, options);
        loaded = RadialMenuOptionsStore.Load(path);
        Assert(loaded.Skin == RadialMenuOptions.CadDarkSkin,
            "圆盘CAD深色皮肤没有持久化。" );

        options.Skin = RadialMenuOptions.PurpleGraySkin;
        RadialMenuOptionsStore.Save(path, options);
        loaded = RadialMenuOptionsStore.Load(path);
        Assert(loaded.Skin == RadialMenuOptions.PurpleGraySkin,
            "圆盘紫灰皮肤没有持久化。" );

        configuredSlot.Name = "快速拉伸";
        configuredSlot.CommandAlias = "KS";
        RadialMenuOptionsStore.Save(path, options);
        loaded = RadialMenuOptionsStore.Load(path);
        Assert(loaded.Items.Single(item => item.Ring == 1 && item.Position == 1) is { Name: "", CommandAlias: "" } &&
               RadialMenuOptions.IsUnsafeExternalCommandAlias(" .LS_CMD_KS_SMART ") &&
               !RadialMenuOptions.IsUnsafeExternalCommandAlias("DCMB"),
            "圆盘没有清除或阻止已知会导致宿主崩溃的外部拉伸命令。");

        loaded.Radius = 50;
        AssertThrows<InvalidOperationException>(loaded.Validate, "非法圆盘大小未被拒绝。");
        loaded.Radius = 220;
        var emptySlot = loaded.Items.Single(item => item.Ring == 1 && item.Position == 1);
        emptySlot.Name = string.Empty;
        emptySlot.CommandAlias = string.Empty;
        loaded.Validate();
        emptySlot.Name = "只有名称";
        AssertThrows<InvalidOperationException>(loaded.Validate, "圆盘空位置应允许留空，但名称和命令只填一项必须拒绝。");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void RadialMenuSurfaceAndCommandAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "RadialMenuCommands.cs"));
    var control = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "RadialMenuControl.cs"));
    var window = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "RadialMenuWindow.cs"));
    var panelCode = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));
    var theme = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Themes", "ToolboxTheme.xaml"));

    Assert(panel.Contains("x:Name=\"RadialMenuModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"圆盘\"", StringComparison.Ordinal) &&
           panel.Contains("启用圆盘工具", StringComparison.Ordinal) &&
           panel.Contains("执行命令后自动关闭圆盘", StringComparison.Ordinal) &&
           panel.Contains("显示第三圈", StringComparison.Ordinal) &&
           panel.Contains("快捷命令配置", StringComparison.Ordinal) &&
           panel.Contains("保存并应用配置", StringComparison.Ordinal),
        "圆盘导航、开关、尺寸透明度或48项配置入口不完整。");
    Assert(commands.Contains("DC_CMD_RADIAL_MENU", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"圆盘\", \"打开圆盘工具\", \"DCYP\")]", StringComparison.Ordinal) &&
           control.Contains("OnRender", StringComparison.Ordinal) &&
           control.Contains("RadialMenuHitTester.HitTest", StringComparison.Ordinal),
        "圆盘底层命令或三圈绘制命中入口不完整。");
    Assert(panel.Contains(RadialMenuOptions.DefaultSkin, StringComparison.Ordinal) &&
           panel.Contains(RadialMenuOptions.CadDarkSkin, StringComparison.Ordinal) &&
           panel.Contains(RadialMenuOptions.PurpleGraySkin, StringComparison.Ordinal) &&
           panelCode.Contains("RadialMenuOptions.CadDarkSkin", StringComparison.Ordinal) &&
           panelCode.Contains("RadialMenuOptions.PurpleGraySkin", StringComparison.Ordinal) &&
           control.Contains("ThemeTextPrimaryBrush", StringComparison.Ordinal) &&
           control.Contains("ThemeTextSecondaryBrush", StringComparison.Ordinal) &&
           control.Contains("ThemeBorderBrush", StringComparison.Ordinal) &&
           control.Contains("RadialCadDarkInnerBrush", StringComparison.Ordinal) &&
           control.Contains("RadialPurpleGrayInnerBrush", StringComparison.Ordinal) &&
           theme.Contains("x:Key=\"RadialCadDarkAccentBrush\"", StringComparison.Ordinal) &&
           theme.Contains("x:Key=\"RadialPurpleGrayAccentBrush\"", StringComparison.Ordinal) &&
           !control.Contains("Color.FromRgb(15, 57, 91)", StringComparison.Ordinal) &&
           !control.Contains("Color.FromRgb(22, 76, 112)", StringComparison.Ordinal) &&
           !control.Contains("Color.FromRgb(30, 98, 128)", StringComparison.Ordinal),
        "圆盘三套皮肤的界面、保存选择或主题令牌不完整。" );
    Assert(panel.Contains("SelectionChanged=\"OnRadialMenuSkinSelectionChanged\"", StringComparison.Ordinal) &&
           panelCode.Contains("private void OnRadialMenuSkinSelectionChanged", StringComparison.Ordinal) &&
           panelCode.Contains("options.Skin = selectedSkin;", StringComparison.Ordinal) &&
           panelCode.Contains("RadialMenuWindowManager.ApplySkin(selectedSkin);", StringComparison.Ordinal) &&
           window.Contains("public static void ApplySkin(string skin)", StringComparison.Ordinal) &&
           window.Contains("InvalidateVisual()", StringComparison.Ordinal),
        "圆盘外观皮肤选择后没有立即写入运行时配置。");
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.RadialMenuCommands))", StringComparison.Ordinal),
        "圆盘命令类没有注册到插件。");
    Assert(panelCode.Contains("FindAncestor<TextBox>", StringComparison.Ordinal) &&
           panelCode.Contains("catch (Exception exception)", StringComparison.Ordinal) &&
           control.Contains("!string.IsNullOrWhiteSpace(item.CommandAlias)", StringComparison.Ordinal),
        "圆盘编辑仍会触发行拖拽、保存异常未被界面承接，或空位置仍会执行命令。");
}

static void RadialMenuMiddleButtonActivationIsRegistered()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var activationPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "RadialMenuMouseActivation.cs");
    Assert(File.Exists(activationPath), "缺少圆盘鼠标中键双击入口。");

    var activation = File.ReadAllText(activationPath);
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));
    Assert(activation.Contains("WmMiddleButtonDoubleClick", StringComparison.Ordinal) &&
           activation.Contains("AcadApplication.PreTranslateMessage += OnPreTranslateMessage", StringComparison.Ordinal) &&
           activation.Contains("AcadApplication.PreTranslateMessage -= OnPreTranslateMessage", StringComparison.Ordinal) &&
           activation.Contains("eventArgs.Handled = true", StringComparison.Ordinal) &&
           activation.Contains("RadialMenuCommands.ShowRadialMenu()", StringComparison.Ordinal),
        "中键双击没有通过AutoCAD消息入口打开圆盘或没有阻止默认缩放范围。");
    Assert(application.Contains("RadialMenuMouseActivation.Initialize()", StringComparison.Ordinal) &&
           application.Contains("RadialMenuMouseActivation.Dispose()", StringComparison.Ordinal),
        "圆盘中键双击监听没有随插件生命周期注册和释放。");
}

static void LispMarketSurfaceContractIsComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var palette = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ToolboxPalette.cs"));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var panelCode = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));

    Assert(palette.Contains("LispMarket", StringComparison.Ordinal),
        "工具箱模块枚举缺少LISP市场。");
    Assert(panel.Contains("x:Name=\"LispMarketModuleButton\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"LISP市场\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"LispMarketModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("AutoCAD 2004~2027", StringComparison.Ordinal) &&
           panel.Contains("方法一：拖入即用（最快）", StringComparison.Ordinal) &&
           panel.Contains("方法二：APPLOAD 命令加载", StringComparison.Ordinal) &&
           panel.Contains("方法三：随 CAD 开机自启（推荐）", StringComparison.Ordinal) &&
           panel.Contains("启动组 (Startup Suite)", StringComparison.Ordinal) &&
           panel.Contains("Header=\"默认本地 LISP 库\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"LispPluginGrid\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"加载选中插件\"", StringComparison.Ordinal) &&
           panel.Contains("LSP、FAS、VLX", StringComparison.Ordinal),
        "LISP市场导航、默认本地库、兼容范围或三种加载指南不完整。");
    Assert(panelCode.Contains("OnLispMarketModuleClick", StringComparison.Ordinal) &&
           panelCode.Contains("ToolboxModule.LispMarket", StringComparison.Ordinal) &&
           panelCode.Contains("LispPluginLibraryService.EnsureDefaultFolder", StringComparison.Ordinal) &&
           panelCode.Contains("OnRefreshLispLibraryClick", StringComparison.Ordinal) &&
           panelCode.Contains("OnOpenLispLibraryClick", StringComparison.Ordinal) &&
           panelCode.Contains("OnLoadSelectedLispClick", StringComparison.Ordinal),
        "LISP市场没有接入本地资源库扫描、刷新、打开目录或加载入口。");
}

static void LispPluginLibraryScansAndBuildsLoadExpression()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcLisp-{Guid.NewGuid():N}");

    try
    {
        var directory = LispPluginLibraryService.EnsureFolder(root);
        var category = Path.Combine(directory, "分类");
        Directory.CreateDirectory(category);
        var lspPath = Path.Combine(directory, "01测试.lsp");
        File.WriteAllText(lspPath, "(princ)");
        File.WriteAllText(Path.Combine(category, "02工具.FAS"), "fas");
        File.WriteAllText(Path.Combine(category, "03扩展.vlx"), "vlx");
        File.WriteAllText(Path.Combine(directory, "忽略.txt"), "text");

        var items = LispPluginLibraryService.Scan(directory);

        Assert(items.Count == 3 &&
               items[0].Name == "01测试" && items[0].Type == "LSP" &&
               items.Any(item => item.RelativePath == Path.Combine("分类", "02工具.FAS")) &&
               items.Any(item => item.Type == "VLX"),
            "本地LISP库没有递归扫描LSP/FAS/VLX，或错误包含了不支持的文件。");

        var expression = LispPluginLibraryService.BuildLoadExpression(lspPath);
        Assert(expression.StartsWith("(load \"", StringComparison.Ordinal) &&
               expression.Contains("/LispPlugins/01测试.lsp\")", StringComparison.Ordinal) &&
               !expression.Contains('\\'),
            "LISP加载表达式没有使用完整正斜杠路径。" );
        AssertThrows<InvalidOperationException>(
            () => LispPluginLibraryService.BuildLoadExpression(Path.Combine(directory, "忽略.txt")),
            "不支持的本地文件类型没有被拒绝。");
    }
    finally
    {
        var resolvedRoot = Path.GetFullPath(root);
        var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}

static void LispPluginCommandsAreUnique()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var libraryPath = Path.Combine(root, "Modules", "LispPlugins");
    var definitions = Directory.EnumerateFiles(libraryPath, "*.lsp", SearchOption.AllDirectories)
        .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                @"\(defun\s+c:([^\s()]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => new
            {
                Command = match.Groups[1].Value.ToUpperInvariant(),
                FileName = Path.GetFileName(path)
            }))
        .ToArray();
    var duplicates = definitions
        .GroupBy(item => item.Command, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => $"{group.Key}: {string.Join(", ", group.Select(item => item.FileName))}")
        .ToArray();

    Assert(duplicates.Length == 0,
        $"本地LISP库存在重复命令，加载顺序会覆盖功能：{string.Join("; ", duplicates)}");
}

static void FrameToolOptionsAndPlacementAreValid()
{
    var options = new FrameToolOptions();
    options.Validate();
    Assert(options.FrameMargin == 50 && options.HorizontalGap == 100 && options.VerticalGap == 100 &&
           options.DetailScale == 3 && options.ClipScale == 1,
        "一键图框默认参数与确认界面不一致。");

    var placement = FramePlacementCalculator.Calculate(
        new PrintFrame(0, 0, 1000, 700),
        new PrintFrame(100, 100, 900, 600),
        50);
    Assert(Math.Abs(placement.Scale - 0.9) < 0.000001 &&
           Math.Abs(placement.InsertionX - 50) < 0.000001 &&
           Math.Abs(placement.InsertionY - 35) < 0.000001,
        "图框没有按内容外扩距离和模板内框中心计算统一缩放与插入点。");

    AssertThrows<InvalidOperationException>(() => new FrameToolOptions { FrameMargin = -1 }.Validate(),
        "负数图形到图框距离未被拒绝。");
    AssertThrows<InvalidOperationException>(() => new FrameToolOptions { DetailScale = 0 }.Validate(),
        "无效局部放大倍数未被拒绝。");
}

static void OneClickFrameMergesOverlappingBlockBounds()
{
    var ordinaryGeometry = new PrintFrame(0, 0, 1000, 700);
    var slightlyOutsideBlock = new PrintFrame(-20, 100, 300, 400);

    var frames = FrameContentProcessor.Prepare([slightlyOutsideBlock], ordinaryGeometry);

    Assert(frames.Count == 1 &&
           frames[0] == new PrintFrame(-20, 0, 1000, 700),
        "普通图形与内部图块范围发生重叠时被拆成多个候选，导致重复重叠加框。");

    var separatedBlock = new PrintFrame(1200, 100, 1500, 400);
    var separatedFrames = FrameContentProcessor.Prepare([separatedBlock], ordinaryGeometry);
    Assert(separatedFrames.Count == 2,
        "彼此分开的独立图块应继续分别生成图框。" );
}

static void FrameTemplateLibraryScansDwgFiles()
{
    var root = Path.Combine(Path.GetTempPath(), $"dc-frame-library-{Guid.NewGuid():N}");
    try
    {
        var directory = FrameTemplateLibraryService.EnsureFolder(root);
        File.WriteAllText(Path.Combine(directory, "设计图框.dwg"), "test");
        File.WriteAllText(Path.Combine(directory, "忽略.txt"), "test");
        var external = Path.Combine(root, "外部图框.dwg");
        File.WriteAllText(external, "test");

        var items = FrameTemplateLibraryService.Scan(directory, external);
        Assert(items.Count == 2 &&
               items.Any(item => item.Name == "设计图框") &&
               items.Any(item => item.Name == "外部图框" && item.IsExternal),
            "图框模板目录没有只扫描DWG，或外部选择没有加入列表。");
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void OneClickFrameSurfaceAndCommandsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "FrameCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    Assert(panel.Contains("x:Name=\"OneClickFrameModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"一键图框\"", StringComparison.Ordinal) &&
           panel.Contains("选择 DWG 模板", StringComparison.Ordinal) &&
           panel.Contains("一键加框", StringComparison.Ordinal) &&
           panel.Contains("图框对齐", StringComparison.Ordinal) &&
           panel.Contains("局部放大", StringComparison.Ordinal) &&
           panel.Contains("局部截图", StringComparison.Ordinal),
        "一键图框模块或截图中确认的操作入口不完整。");
    Assert(commands.Contains("DC_CMD_FRAME_AUTO", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_FRAME_ALIGN", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_DETAIL_DRAW", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_DETAIL_CLIP", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"一键图框\", \"一键加框\", \"DCTK\")]", StringComparison.Ordinal),
        "一键图框四类底层命令或DC前缀快捷键不完整。");
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.FrameCommands))", StringComparison.Ordinal),
        "图框命令类没有注册到插件。");
}

static void HardwareOptionsValidateAndCalculateHingeCount()
{
    Assert(HardwareOptions.TryCreate(
            "100", "10", "100", "100", "100", "2", "3", "4", "5", "6", false, "280", true,
            HingeDimensionMode.Continuous, "80", "25", "25", "35", "35",
            out var options,
            out var error),
        $"默认五金参数未通过验证：{error}");
    Assert(options.GetHingeCount(899) == 2 &&
           options.GetHingeCount(900) == 3 &&
           options.GetHingeCount(1500) == 4 &&
           options.GetHingeCount(2000) == 5 &&
           options.GetHingeCount(2500) == 6,
        "门板高度分档没有按界面规则计算铰链数量。");
    Assert(!HardwareOptions.TryCreate(
            "-1", "10", "100", "100", "100", "2", "3", "4", "5", "6", false, "280", true,
            HingeDimensionMode.Continuous, "80", "25", "25", "35", "35",
            out _,
            out _),
        "负数挂衣杆离顶高度未被拒绝。");
    Assert(!HardwareOptions.TryCreate(
            "100", "10", "100", "100", "100", "2", "3", "4", "5", "6", false, "280", true,
            HingeDimensionMode.Continuous, "80", "25", "25", "0", "-1",
            out _,
            out _),
        "非法铰链类型文字高度或间距未被拒绝。");
}

static void HangingRodLayoutUsesClearWidthAndTopOffset()
{
    var placement = HardwareLayoutCalculator.CalculateHangingRod(
        left: 100,
        right: 900,
        top: 2200,
        topOffset: 100,
        sideClearance: 4);

    Assert(placement.Left == 104 && placement.Right == 896 && placement.Width == 792,
        "挂衣杆没有按柜内净宽左右各减4mm。");
    Assert(placement.Y == 2100, "挂衣杆离顶高度没有参与落位计算。");
}

static void HangingRodFindsNearestCoveringTopLine()
{
    var selected = new HorizontalLineSpan(100, 900, 800);
    var top = HardwareLayoutCalculator.FindNearestCoveringLineAbove(
        selected,
        [
            new HorizontalLineSpan(0, 1000, 2200),
            new HorizontalLineSpan(120, 880, 1500),
            new HorizontalLineSpan(100, 900, 1800),
            new HorizontalLineSpan(0, 1000, 700)
        ]);

    Assert(top == new HorizontalLineSpan(100, 900, 1800),
        "没有忽略未覆盖/下方横线并选择最近的上方覆盖横线。");
    Assert(HardwareLayoutCalculator.FindNearestCoveringLineAbove(
            selected,
            [new HorizontalLineSpan(101, 900, 1800)]) is null,
        "未完整覆盖所选横线的候选线被错误识别为顶线。");
}

static void HangingRodUsesClickedBayBoundaries()
{
    var bay = HardwareLayoutCalculator.FindHorizontalBayAt(
        new HorizontalLineSpan(0, 1800, 500),
        pickedX: 750,
        [
            new VerticalLineSpan(0, 0, 2200),
            new VerticalLineSpan(600, 0, 2200),
            new VerticalLineSpan(700, 600, 2200),
            new VerticalLineSpan(1200, 0, 2200),
            new VerticalLineSpan(1800, 0, 2200)
        ]);

    Assert(bay == new HorizontalLineSpan(600, 1200, 500),
        "没有按点击点所在位置选择左右最近且跨过基准线的竖线。");
}

static void HangingRodFitsCommittedDynamicExtents()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "HardwareCommands.cs");
    var commands = File.ReadAllText(commandsPath);

    Assert(commands.Contains("FitHangingRodToPlacement(database, referenceId, placement)", StringComparison.Ordinal),
        "挂衣杆仍在插入事务内按未提交的动态块外包框校正。");
    Assert(commands.Contains("placement.Width - measuredWidth", StringComparison.Ordinal) &&
           commands.Contains("placement.Left - finalExtents.MinPoint.X", StringComparison.Ordinal),
        "挂衣杆没有按提交后的最终可见宽度修正动态参数并对齐最终左端点。");
}

static void HingeLayoutUsesFixedSecondSpacing()
{
    var offsets = HardwareLayoutCalculator.CalculateHingeOffsets(
        panelHeight: 2000,
        hingeCount: 5,
        topOffset: 100,
        bottomOffset: 100,
        useFixedSecondSpacing: true,
        fixedSecondSpacing: 280);

    var expected = new[] { 100d, 380d, 886.6666666666666d, 1393.3333333333333d, 1900d };
    Assert(offsets.Count == expected.Length &&
           offsets.Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 0.000001),
        "高门板第二铰链没有固定在首个铰链下方280mm，或其余铰链没有均布到底部铰链。");
}

static void HingeDefaultsToSplitFromEnds()
{
    Assert(HardwareOptions.Default.HingeDimensionMode == HingeDimensionMode.SplitFromEnds,
        "铰链默认标注仍是连续尺寸链，没有使用门顶和门底两个加工基准。");
}

static void HingeDimensionDefaultsMatchPanelInputs()
{
    var defaults = HardwareOptions.Default;
    Assert(defaults.HingeDimensionMode == HingeDimensionMode.SplitFromEnds &&
           defaults.HingeDimensionOffset == 60 &&
           defaults.HingeDimensionLineSpacing == 80 &&
           defaults.HingeDimensionTextHeight == 50,
        "铰链尺寸默认值不是上下定位 / 60 / 80 / 50。");

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panel = File.ReadAllText(panelPath);
    Assert(panel.Contains("x:Name=\"HingeDimensionModeCombo\"", StringComparison.Ordinal) &&
           panel.Contains("SelectedIndex=\"1\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"HingeDimensionOffsetBox\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"60\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"HingeDimensionLineSpacingBox\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"80\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"HingeDimensionTextHeightBox\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"50\"", StringComparison.Ordinal),
        "门板抽面的铰链尺寸输入框未使用上下定位 / 60 / 80 / 50 默认值。");
}

static void HingeCentersStayInsideDoor()
{
    var leftCenter = HardwareLayoutCalculator.CalculateHingeCenterX(
        left: 100,
        right: 700,
        isLeft: true,
        edgeOffset: 23,
        symbolHalfWidth: 19.5);
    var rightCenter = HardwareLayoutCalculator.CalculateHingeCenterX(
        left: 100,
        right: 700,
        isLeft: false,
        edgeOffset: 23,
        symbolHalfWidth: 19.5);

    Assert(leftCenter == 123 && rightCenter == 677,
        "铰链中心没有从所选侧向门板内部偏移。");
    Assert(leftCenter - 19.5 >= 100 && rightCenter + 19.5 <= 700,
        "铰链圆或十字线仍可能超出门板矩形。");
}

static void HingeAvoidanceUsesPanelShelfOverlap()
{
    var panel = new DoorPanelLeaf(-30, 503, -18, 2018, true);
    var moved = HardwareLayoutCalculator.MoveHingeOffsetsAwayFromShelves(
        panel,
        [100, 700, 1936],
        [new HorizontalLineSpan(0, 500, 1918)],
        clearance: 40,
        minimumSpacing: 50,
        minimumEdgeOffset: 19.5);

    Assert(moved.SequenceEqual([60d, 700d, 1936d]),
        "盖板延伸使铰链中心越过层板端点后，层板与门板仍相交却没有触发避让。" );

    var unchanged = HardwareLayoutCalculator.MoveHingeOffsetsAwayFromShelves(
        panel,
        [100, 700, 1936],
        [new HorizontalLineSpan(600, 900, 1918)],
        clearance: 40,
        minimumSpacing: 50,
        minimumEdgeOffset: 19.5);
    Assert(unchanged.SequenceEqual([100d, 700d, 1936d]),
        "与门板没有水平交集的层板不应影响铰链位置。" );
}

static void HingeAvoidanceUsesInnerOpeningScope()
{
    var method = typeof(HardwareLayoutCalculator)
        .GetMethods()
        .SingleOrDefault(candidate =>
        {
            var parameters = candidate.GetParameters();
            return candidate.Name == "MoveHingeOffsetsAwayFromShelves" &&
                   parameters.Length == 8 &&
                   parameters[1].ParameterType == typeof(DoorPanelBounds);
        });
    Assert(method is not null, "铰链避让尚未把柜体内空范围与延伸后的门板外形分开。" );

    var panel = new DoorPanelLeaf(-30, 530, -18, 2018, true);
    var innerBounds = new DoorPanelBounds(0, 500, 0, 2000);
    var inside = (IReadOnlyList<double>)method!.Invoke(
        null,
        [
            panel,
            innerBounds,
            new double[] { 100 },
            new HorizontalLineSpan[] { new(0, 500, 1918) },
            40d,
            50d,
            19.5d,
            0.000001d
        ])!;
    Assert(inside.SequenceEqual([60d]),
        "柜内层板没有参与延伸门板的铰链避让计算。" );

    var coverOnly = (IReadOnlyList<double>)method.Invoke(
        null,
        [
            panel,
            innerBounds,
            new double[] { 100 },
            new HorizontalLineSpan[] { new(-30, -1, 1918) },
            40d,
            50d,
            19.5d,
            0.000001d
        ])!;
    Assert(coverOnly.SequenceEqual([100d]),
        "仅落在盖板延伸范围内的层板不应改变铰链位置。" );

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelCommands.cs");
    var commands = File.ReadAllText(commandsPath);
    Assert(commands.Contains("DoorPanelBounds shelfScope", StringComparison.Ordinal) &&
           commands.Contains("innerBounds", StringComparison.Ordinal),
        "DCMB 正式生成仍未把原始柜体内空范围传入铰链避让链路。" );
}

static void DoorPanelHingesCompensateVerticalExtensions()
{
    var method = typeof(HardwareLayoutCalculator).GetMethod("CalculateInnerOpeningHingeOffsets");
    Assert(method is not null, "缺少按柜内定位并补偿上下门板延伸的铰链位置计算。" );

    var panel = new DoorPanelLeaf(0, 500, -15, 915, true);
    var innerBounds = new DoorPanelBounds(0, 500, 0, 895);
    var offsets = (IReadOnlyList<double>)method!.Invoke(
        null,
        [panel, innerBounds, HardwareOptions.Default])!;

    Assert(offsets.Count == 2,
        "铰链数量仍按延伸后的门板高度计算，而不是按柜内高度计算。" );
    Assert(offsets.SequenceEqual([120d, 815d]),
        "铰链没有在柜内距顶/距底基础上分别加上门板上下延伸值。" );
    Assert(Math.Abs(panel.Top - offsets[0] - (innerBounds.Top - 100)) < 0.000001 &&
           Math.Abs(panel.Top - offsets[^1] - (innerBounds.Bottom + 100)) < 0.000001,
        "上下延伸后铰链的世界坐标没有保持柜内定位。" );
}

static void ThinEntityBoundsAreRecognizedAsShelf()
{
    var method = typeof(HardwareLayoutCalculator).GetMethod("CreateShelfSpanFromBounds");
    Assert(method is not null, "核心层板识别器尚未支持实体几何包围盒。" );

    var shelf = (HorizontalLineSpan?)method!.Invoke(null, [0d, 900d, 990d, 1010d]);
    Assert(shelf == new HorizontalLineSpan(0, 900, 1000),
        "宽而薄的实体没有按包围盒中心线识别为层板。" );

    var tallEntity = (HorizontalLineSpan?)method.Invoke(null, [0d, 900d, 900d, 1100d]);
    var narrowEntity = (HorizontalLineSpan?)method.Invoke(null, [0d, 80d, 990d, 1010d]);
    Assert(tallEntity is null && narrowEntity is null,
        "普通高实体或短实体被误识别为层板。" );
}

static void DoorPanelShelfCollectionUsesEntityBounds()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelCommands.cs");
    var commands = File.ReadAllText(commandsPath);
    var start = commands.IndexOf("private static IReadOnlyList<HorizontalLineSpan> CollectHorizontalSpans", StringComparison.Ordinal);
    var end = commands.IndexOf("private static DbPolyline CreatePanelOutline", start, StringComparison.Ordinal);
    var collection = commands[start..end];

    Assert(collection.Contains("entity.GeometricExtents", StringComparison.Ordinal) &&
           collection.Contains("CreateShelfSpanFromBounds", StringComparison.Ordinal),
        "门板层板收集器仍只读取直线/轻量多段线，块参照或薄板实体会被漏掉。" );
}

static void HingeContinuousDimensionsCoverAllIntervals()
{
    var dimensions = HardwareLayoutCalculator.CalculateHingeDimensions(
        2000,
        [100, 380, 900, 1900],
        HingeDimensionMode.Continuous);
    var expected = new[]
    {
        new HingeDimensionSegment(0, 100, 0),
        new HingeDimensionSegment(100, 380, 0),
        new HingeDimensionSegment(380, 900, 0),
        new HingeDimensionSegment(900, 1900, 0),
        new HingeDimensionSegment(1900, 2000, 0)
    };

    Assert(dimensions.SequenceEqual(expected), "连续标注没有形成从门顶到门底的完整尺寸链。");
}

static void HingeSplitDimensionsUseNearestPanelEnd()
{
    var dimensions = HardwareLayoutCalculator.CalculateHingeDimensions(
        2000,
        [100, 380, 1000, 1500, 1900],
        HingeDimensionMode.SplitFromEnds);
    var expected = new[]
    {
        new HingeDimensionSegment(0, 100, 0),
        new HingeDimensionSegment(0, 380, 1),
        new HingeDimensionSegment(0, 1000, 2),
        new HingeDimensionSegment(2000, 1900, 0),
        new HingeDimensionSegment(2000, 1500, 1)
    };

    Assert(dimensions.SequenceEqual(expected), "上下定位没有按就近端点分组，或正中铰链未归顶部。");
}

static void HingeDimensionsUseUnitScale()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "HardwareCommands.cs");
    var commands = File.ReadAllText(commandsPath);

    Assert(commands.Contains("Dimscale = 1", StringComparison.Ordinal),
        "铰链标注仍会继承当前标注样式的全局比例，文字、箭头和间隙可能被二次放大。");
}

static void DoorOpeningLinesConvergeOnHingeSide()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelCommands.cs");
    var commands = File.ReadAllText(commandsPath);
    var methodStart = commands.IndexOf("private static IEnumerable<Line> CreateOpeningLines", StringComparison.Ordinal);
    var methodEnd = commands.IndexOf("private static string? TryEnsureDashedLinetype", methodStart, StringComparison.Ordinal);
    var method = commands[methodStart..methodEnd];

    Assert(method.Contains("var openingEdgeX = panel.HingeOnLeft ? panel.Right : panel.Left;", StringComparison.Ordinal) &&
           method.Contains("var center = new Point3d(hingeX", StringComparison.Ordinal) &&
           method.Contains("new Point3d(openingEdgeX, panel.Top", StringComparison.Ordinal) &&
           method.Contains("new Point3d(openingEdgeX, panel.Bottom", StringComparison.Ordinal),
        "开门虚线没有从自由边出发并向铰链侧中点汇合。");
}

static void HingeDimensionsStayInsideDoorLeaf()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "DoorPanelCommands.cs");
    var commands = File.ReadAllText(commandsPath);
    var methodStart = commands.IndexOf("private static void WritePanelHinges", StringComparison.Ordinal);
    var methodEnd = commands.IndexOf("private static IReadOnlyList<HorizontalLineSpan> CollectHorizontalSpans", methodStart, StringComparison.Ordinal);
    var method = commands[methodStart..methodEnd];

    Assert(method.Contains("var inside = panel.HingeOnLeft ? 1d : -1d;", StringComparison.Ordinal) &&
           method.Contains("sideX + inside * lineOffset", StringComparison.Ordinal),
        "铰链标尺仍向门板外侧偏移，没有画在门内。");
}

static void HingeCommandSelectsDoorEdges()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "HardwareCommands.cs");
    var panelCodePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs");
    var commands = File.ReadAllText(commandsPath);
    var panelCode = File.ReadAllText(panelCodePath);

    Assert(commands.Contains("TrySelectHingeDoorEdges", StringComparison.Ordinal) &&
           commands.Contains("请选择门板上边线", StringComparison.Ordinal) &&
           commands.Contains("请选择门板下边线", StringComparison.Ordinal) &&
           !commands.Contains("请选择门板闭合矩形", StringComparison.Ordinal),
        "DCJL 仍要求选择闭合矩形，没有改为依次选择门板上下边线。");
    Assert(panelCode.Contains("先选择门板上边线，再选择下边线", StringComparison.Ordinal) &&
           !panelCode.Contains("请选择门板闭合矩形", StringComparison.Ordinal),
        "五金件面板仍提示选择闭合矩形，与 DCJL 的选边操作不一致。");
}

static void StandaloneHingeDimensionsUsePanelOptions()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "HardwareCommands.cs");
    var commands = File.ReadAllText(commandsPath);
    var methodStart = commands.IndexOf("private static void WriteHinges", StringComparison.Ordinal);
    var methodEnd = commands.IndexOf("private static bool TrySelectHangingRodLines", methodStart, StringComparison.Ordinal);
    var method = commands[methodStart..methodEnd];

    Assert(method.Contains("options.HingeDimensionMode", StringComparison.Ordinal) &&
           method.Contains("options.HingeDimensionOffset", StringComparison.Ordinal) &&
           method.Contains("options.HingeDimensionLineSpacing", StringComparison.Ordinal) &&
           method.Contains("options.HingeDimensionTextHeight", StringComparison.Ordinal),
        "JL 单独插入铰链没有完整复用面板的尺寸标注参数。");
    Assert(method.Contains("var inside = isLeft ? 1d : -1d;", StringComparison.Ordinal) &&
           method.Contains("sideX + inside * lineOffset", StringComparison.Ordinal),
        "JL 单独插入铰链的标尺仍然画在门外。");
}

static void HingeLabelsSupportMultipleCircleSelection()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "HardwareCommands.cs");
    var menuPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "HingeTypeMenuWindow.xaml");
    var menuCodePath = menuPath + ".cs";
    var commands = File.ReadAllText(commandsPath);
    var methodStart = commands.IndexOf("public static void LabelHinges", StringComparison.Ordinal);
    var methodEnd = commands.IndexOf("private static void RunHingeCommand", methodStart, StringComparison.Ordinal);
    var method = commands[methodStart..methodEnd];

    Assert(method.Contains("GetSelection", StringComparison.Ordinal) &&
           method.Contains("SelectionFilter", StringComparison.Ordinal) &&
           method.Contains("DxfCode.Start", StringComparison.Ordinal) &&
           method.Contains("DxfCode.LayerName", StringComparison.Ordinal),
        "HJL 仍未通过类型和图层过滤框选多个铰链圆。");
    Assert(method.Contains("TryShowHingeTypeMenu", StringComparison.Ordinal) &&
           method.Contains("WriteHingeTypeLabels", StringComparison.Ordinal),
        "HJL 未在结束多选后打开鼠标旁菜单并批量生成文字标记。");
    Assert(!commands.Contains("PromptKeywordOptions", StringComparison.Ordinal) &&
           !commands.Contains("GetKeywords", StringComparison.Ordinal),
        "HJL 仍依赖命令行关键字，动态输入关闭提示时无法在鼠标旁选择类型。");
    Assert(File.Exists(menuPath) && File.Exists(menuCodePath),
        "缺少鼠标旁铰链类型菜单窗口。");
    var menu = File.ReadAllText(menuPath);
    var menuCode = File.ReadAllText(menuCodePath);
    Assert(menu.Contains("内嵌 (D)", StringComparison.Ordinal) &&
           menu.Contains("半盖 (X)", StringComparison.Ordinal) &&
           menu.Contains("全盖 (Z)", StringComparison.Ordinal) &&
           menu.Contains("WindowStartupLocation=\"Manual\"", StringComparison.Ordinal),
        "鼠标旁菜单缺少内嵌、半盖、全盖选项或手动定位设置。");
    Assert(menuCode.Contains("GetCursorPos", StringComparison.Ordinal) &&
           menuCode.Contains("SelectedHingeType", StringComparison.Ordinal) &&
           commands.Contains("AcadApplication.ShowModalWindow", StringComparison.Ordinal),
        "铰链类型菜单没有读取鼠标位置或接入 AutoCAD 模态窗口。");
}

static void HingeShortcutQueuesCoreCommand()
{
    var item = CreateItem("标记铰链种类", "DC_CMD_HINGE_LABEL", "DCHJL");
    var script = AutoLispShortcutGenerator.Generate(
        new ShortcutConfiguration { ShortcutItems = [item] });

    Assert(script.Contains("vla-SendCommand", StringComparison.Ordinal) &&
           script.Contains(".DC_CMD_HINGE_LABEL ", StringComparison.Ordinal),
        "DCHJL 仍在 Visual LISP 调用栈中同步执行弹窗型底层命令。" );
    Assert(!script.Contains("(vl-cmdf \".DC_CMD_HINGE_LABEL\")", StringComparison.Ordinal),
        "DCHJL 仍通过 vl-cmdf 嵌套调用，键盘入口可能无法进入鼠标菜单流程。" );
}

static void NativeAliasesBypassLispLoadOrder()
{
    var sourceRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "YiZhiMao.Dc.Plugin"));
    var doorCommands = File.ReadAllText(Path.Combine(sourceRoot, "DoorPanelCommands.cs"));
    var hardwareCommands = File.ReadAllText(Path.Combine(sourceRoot, "HardwareCommands.cs"));

    Assert(doorCommands.Contains("[CommandMethod(\"DCMB\", CommandFlags.Modal)]", StringComparison.Ordinal) &&
           doorCommands.Contains("public static void GenerateElevationDoorShortcut()", StringComparison.Ordinal) &&
           doorCommands.Contains("GenerateElevationDoor();", StringComparison.Ordinal),
        "DCMB 仍完全依赖文档内 LSP，快捷脚本未加载时无法启动两点门板命令。" );
    Assert(hardwareCommands.Contains("[CommandMethod(\"DCHJL\", CommandFlags.Modal)]", StringComparison.Ordinal) &&
           hardwareCommands.Contains("public static void LabelHingesShortcut()", StringComparison.Ordinal) &&
           hardwareCommands.Contains("LabelHinges();", StringComparison.Ordinal),
        "DCHJL 缺少原生命令兜底，仍可能被雷神 HJL 的 LSP 定义覆盖。" );
}

static void HingeTypeLabelsUseConfigurableInsidePlacement()
{
    var optionsType = typeof(HardwareOptions);
    var height = optionsType.GetProperty("HingeTypeTextHeight");
    var spacing = optionsType.GetProperty("HingeTypeTextSpacing");
    Assert(height is not null && spacing is not null,
        "五金参数缺少铰链类型文字高度或文字间距。" );
    Assert((double)height!.GetValue(HardwareOptions.Default)! == 35 &&
           (double)spacing!.GetValue(HardwareOptions.Default)! == 35,
        "铰链类型文字高度和间距默认值不是 35。" );

    var placementMethod = typeof(HardwareLayoutCalculator)
        .GetMethods()
        .SingleOrDefault(candidate =>
            candidate.Name == "CalculateHingeTypeLabelPlacement" &&
            candidate.GetParameters().Length == 5);
    Assert(placementMethod is not null, "缺少把铰链类型文字放向门内的定位计算。" );
    var leftPlacement = placementMethod!.Invoke(null, [23d, 17.5d, 0d, 500d, 35d])!;
    var rightPlacement = placementMethod.Invoke(null, [477d, 17.5d, 0d, 500d, 35d])!;
    var xProperty = leftPlacement.GetType().GetProperty("X")!;
    var extendsRightProperty = leftPlacement.GetType().GetProperty("ExtendsRight")!;
    Assert(Math.Abs((double)xProperty.GetValue(leftPlacement)! - 75.5) < 0.000001 &&
           (bool)extendsRightProperty.GetValue(leftPlacement)! &&
           Math.Abs((double)xProperty.GetValue(rightPlacement)! - 424.5) < 0.000001 &&
           !(bool)extendsRightProperty.GetValue(rightPlacement)!,
        "铰链类型文字锚点没有按圆半径和设定间距向门内展开。" );

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var panelCode = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "HardwareCommands.cs"));
    Assert(panel.Contains("x:Name=\"HingeTypeTextHeightBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"HingeTypeTextSpacingBox\"", StringComparison.Ordinal) &&
           panel.Contains("Text=\"35\"", StringComparison.Ordinal),
        "门板抽面面板缺少铰链类型文字高度/间距输入框或默认值。" );
    Assert(panelCode.Contains("HingeTypeTextHeightBox.Text", StringComparison.Ordinal) &&
           panelCode.Contains("HingeTypeTextSpacingBox.Text", StringComparison.Ordinal),
        "面板没有加载并保存铰链类型文字参数。" );
    Assert(commands.Contains("AttachmentPoint.MiddleLeft", StringComparison.Ordinal) &&
           commands.Contains("AttachmentPoint.MiddleRight", StringComparison.Ordinal) &&
           commands.Contains("CalculateHingeTypeLabelPlacement", StringComparison.Ordinal),
        "HJL 文字仍未根据对应门板的铰链侧向门内放置。" );
}

static void HardwareSurfaceContractIsComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var panelCodePath = panelPath + ".cs";
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "HardwareCommands.cs");
    var applicationPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs");

    var panel = File.ReadAllText(panelPath);
    var panelCode = File.ReadAllText(panelCodePath);
    Assert(panel.Contains("Text=\"五金件\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"HardwareModuleView\"", StringComparison.Ordinal),
        "左侧导航或五金件模块视图缺失。");
    foreach (var header in new[] { "挂衣杆设置", "灯带设置" })
    {
        Assert(panel.Contains($"Header=\"{header}\"", StringComparison.Ordinal), $"五金件缺少{header}页签。");
    }

    Assert(panelCode.Contains("HardwareLibraryService.EnsureDefaultFolders", StringComparison.Ordinal) &&
           panelCode.Contains("HardwareLibraryService.Scan", StringComparison.Ordinal),
        "五金件面板没有定位并扫描bundle内的Modules素材库。");
    Assert(File.Exists(commandsPath), "缺少五金件底层命令文件。");
    var commands = File.ReadAllText(commandsPath);
    Assert(commands.Contains("[CommandMethod(\"DC_CMD_HANGING_ROD\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"五金件\", \"插入挂衣杆\", \"DCYG\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DC_CMD_LIGHT_STRIP\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"五金件\", \"插入灯带\", \"DCDD\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DC_CMD_HINGE_INSERT\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"门板抽面\", \"插入铰链\", \"DCJL\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DC_CMD_HINGE_LABEL\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"门板抽面\", \"标记铰链种类\", \"DCHJL\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DCHJL\", CommandFlags.Modal)]", StringComparison.Ordinal),
        "五金件及门板铰链命令没有使用DC_CMD_底层命令、正确分类和预期默认快捷键。");
    Assert(panel.Contains("Click=\"OnInsertHingesClick\"", StringComparison.Ordinal) &&
           panel.Contains("Click=\"OnLabelHingesClick\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"HingeDimensionModeCombo\"", StringComparison.Ordinal) &&
           panel.Contains("Tag=\"Continuous\"", StringComparison.Ordinal) &&
           panel.Contains("Tag=\"SplitFromEnds\"", StringComparison.Ordinal) &&
           panelCode.Contains("RunHingeCommand(\"DC_CMD_HINGE_INSERT\"", StringComparison.Ordinal) &&
           panelCode.Contains("RunHingeCommand(\"DC_CMD_HINGE_LABEL\"", StringComparison.Ordinal),
        "铰链面板按钮尚未接通JL和DCHJL底层命令。");
    Assert(commands.Contains("TrySelectHangingRodLines", StringComparison.Ordinal) &&
           commands.Contains("FindHorizontalBayAt", StringComparison.Ordinal) &&
           commands.Contains("CalculateHingeDimensions", StringComparison.Ordinal) &&
           commands.Contains("CalculateHingeCenterX", StringComparison.Ordinal) &&
           commands.Contains("new AlignedDimension", StringComparison.Ordinal) &&
           commands.Contains("GetPoint3dAt", StringComparison.Ordinal),
        "挂衣杆横线识别或铰链尺寸实体生成尚未接通底层命令。");
    Assert(File.ReadAllText(applicationPath).Contains(
            "[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.HardwareCommands))]",
            StringComparison.Ordinal),
        "五金件命令类未注册到插件入口。");
}

static void SuperLibraryCatalogScansCategoriesAndPreviews()
{
    var root = Path.Combine(Path.GetTempPath(), $"dc-super-library-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        var wardrobe = Directory.CreateDirectory(Path.Combine(root, "2门衣柜")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "空分类"));
        File.WriteAllBytes(Path.Combine(wardrobe, "标准柜.dwg"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(wardrobe, "标准柜.png"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(wardrobe, "备用.DWG"), [7]);
        File.WriteAllBytes(Path.Combine(wardrobe, "自动备份.bak"), [8]);
        File.WriteAllBytes(Path.Combine(root, "根目录组件.dwg"), [9]);

        var categories = SuperLibraryCatalog.Scan(root);
        var category = categories.Single(item => item.Name == "2门衣柜");
        Assert(categories.Any(item => item.Name == "空分类" && item.Items.Count == 0),
            "超级图库没有保留空分类。" );
        Assert(categories.Any(item => item.Name == SuperLibraryCatalog.UnclassifiedCategoryName && item.Items.Count == 1),
            "超级图库根目录DWG没有归入未分类。" );
        Assert(category.Items.Count == 2 &&
               category.Items.Any(item => item.Name == "标准柜" && item.PreviewPath?.EndsWith("标准柜.png", StringComparison.OrdinalIgnoreCase) == true) &&
               category.Items.All(item => !item.DwgPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)),
            "超级图库没有按DWG扫描或匹配同名预览图。" );
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void SuperLibraryCatalogProtectsRootBoundary()
{
    var root = Path.Combine(Path.GetTempPath(), $"dc-super-library-safe-{Guid.NewGuid():N}");
    var outside = Path.Combine(Path.GetTempPath(), $"dc-super-library-outside-{Guid.NewGuid():N}.dwg");
    Directory.CreateDirectory(root);
    try
    {
        var original = SuperLibraryCatalog.CreateCategory(root, "原分类");
        var renamed = SuperLibraryCatalog.RenameCategory(root, "原分类", "新分类");
        Assert(!Directory.Exists(original) && Directory.Exists(renamed), "超级图库分类没有安全改名。" );

        var dwgPath = Path.Combine(renamed, "可删除.dwg");
        var previewPath = Path.Combine(renamed, "可删除.jpg");
        File.WriteAllBytes(dwgPath, [1]);
        File.WriteAllBytes(previewPath, [2]);
        Assert(SuperLibraryCatalog.DeleteItems(root, [dwgPath]) == 1 &&
               !File.Exists(dwgPath) && !File.Exists(previewPath),
            "超级图库没有成对删除DWG和同名预览图。" );
        SuperLibraryCatalog.DeleteCategory(root, "新分类");
        Assert(!Directory.Exists(renamed), "空的超级图库分类没有删除。" );

        var retained = Path.Combine(root, "边界校验失败时保留.dwg");
        File.WriteAllBytes(retained, [3]);
        File.WriteAllBytes(outside, [4]);
        AssertThrows<InvalidOperationException>(
            () => SuperLibraryCatalog.DeleteItems(root, [retained, outside]),
            "超级图库允许删除根目录外文件。" );
        Assert(File.Exists(retained), "超级图库批量删除没有先校验全部路径，造成了部分文件被提前删除。" );
        AssertThrows<InvalidOperationException>(
            () => SuperLibraryCatalog.CreateCategory(root, ".."),
            "超级图库允许创建路径穿越分类。" );
    }
    finally
    {
        DeleteTestDirectory(root);
        if (File.Exists(outside))
        {
            File.Delete(outside);
        }
    }
}

static void SuperLibrarySurfaceAndCommandsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var panelCode = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var palette = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ToolboxPalette.cs"));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "SuperLibraryCommands.cs");
    var windowPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "SuperLibraryWindow.xaml");
    var windowCodePath = windowPath + ".cs";
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    var batchIndex = panel.IndexOf("x:Name=\"BatchPrintModuleButton\"", StringComparison.Ordinal);
    var libraryIndex = panel.IndexOf("x:Name=\"SuperLibraryModuleButton\"", StringComparison.Ordinal);
    var statisticsIndex = panel.IndexOf("x:Name=\"StatisticsModuleButton\"", StringComparison.Ordinal);
    Assert(batchIndex >= 0 && libraryIndex > batchIndex && statisticsIndex > libraryIndex &&
           panel.Contains("x:Name=\"SuperLibraryModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("打开超级图库 (DCTK)", StringComparison.Ordinal),
        "超级图库没有位于批量打印下方或缺少面板入口。" );
    Assert(panelCode.Contains("ToolboxModule.SuperLibrary", StringComparison.Ordinal) &&
           palette.Contains("SuperLibrary", StringComparison.Ordinal),
        "超级图库没有接入工具箱导航状态。" );

    Assert(File.Exists(commandsPath) && File.Exists(windowPath) && File.Exists(windowCodePath),
        "超级图库命令或窗口文件缺失。" );
    var commands = File.ReadAllText(commandsPath);
    var window = File.ReadAllText(windowPath);
    var windowCode = File.ReadAllText(windowCodePath);
    Assert(commands.Contains("[ShortcutCommand(\"超级图库\", \"打开超级图库\", \"DCTK\")]", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"超级图库\", \"保存选中图形到图库\", \"DCBCT\")]", StringComparison.Ordinal) &&
           commands.Contains("EntityJig", StringComparison.Ordinal) &&
           commands.Contains("ReadDwgFile", StringComparison.Ordinal) &&
           commands.Contains("Wblock", StringComparison.Ordinal),
        "超级图库没有使用DC前缀命令、鼠标预览插入或选中图形保存。" );
    Assert(window.Contains("x:Name=\"CategoryList\"", StringComparison.Ordinal) &&
           window.Contains("x:Name=\"LibraryItemsControl\"", StringComparison.Ordinal) &&
           window.Contains("x:Name=\"PreviewScaleSlider\"", StringComparison.Ordinal) &&
           window.Contains("Content=\"批量删除\"", StringComparison.Ordinal) &&
           windowCode.Contains("SendStringToExecute", StringComparison.Ordinal) &&
           windowCode.Contains("SuperLibraryCatalog.Scan", StringComparison.Ordinal),
        "超级图库窗口缺少分类、预览、批量删除或CAD命令调度。" );
    Assert(application.Contains("[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.SuperLibraryCommands))]", StringComparison.Ordinal) &&
           application.Contains("SuperLibraryWindowManager.Dispose()", StringComparison.Ordinal),
        "超级图库命令没有注册或窗口生命周期没有在插件卸载时清理。" );
}

static void ModuleResourcesAreBundledAndReadDirectly()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var modulesPath = Path.Combine(root, "Modules");
    var projectPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "YiZhiMao.Dc.Plugin.csproj");
    var panelCodePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs");
    var locatorPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginResourceLocator.cs");

    Assert(Directory.Exists(Path.Combine(modulesPath, "HangingRodBlocks")) &&
           Directory.Exists(Path.Combine(modulesPath, "LightStripBlocks")) &&
           Directory.Exists(Path.Combine(modulesPath, "HandleBlocks")),
        "项目根目录的Modules缺少挂衣杆、灯带或抽面拉手资源目录。");

    var project = File.ReadAllText(projectPath);
    Assert(project.Contains("<ModulesRoot>$(MSBuildProjectDirectory)\\..\\..\\Modules</ModulesRoot>",
               StringComparison.Ordinal) &&
           project.Contains("<BundleModuleFiles Include=\"$(ModulesRoot)\\**\\*\" Exclude=\"$(ModulesRoot)\\**\\*.bak\"", StringComparison.Ordinal) &&
           project.Contains("$(BundleContents)\\Modules\\%(RecursiveDir)%(Filename)%(Extension)",
               StringComparison.Ordinal),
        "Modules尚未递归复制到bundle的Contents\\Modules，或没有排除AutoCAD备份文件。");

    var panelCode = File.ReadAllText(panelCodePath);
    var locatorCode = File.ReadAllText(locatorPath);
    Assert(panelCode.Contains("PluginResourceLocator.GetPluginRoot()", StringComparison.Ordinal) &&
           locatorCode.Contains("Directory.GetParent(assemblyDirectory)", StringComparison.Ordinal) &&
           locatorCode.Contains("Path.Combine(parentDirectory, \"Modules\")", StringComparison.Ordinal),
        "五金件面板尚未从插件安装目录的Modules读取资源。");
}

static void BundleBuildCleansAllPreviousContents()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var projectPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "YiZhiMao.Dc.Plugin.csproj");
    var project = File.ReadAllText(projectPath);
    var cleanIndex = project.IndexOf("<RemoveDir Directories=\"$(BundleContents)\"", StringComparison.Ordinal);
    var createIndex = project.IndexOf("<MakeDir Directories=\"$(BundleContents)\"", StringComparison.Ordinal);

    Assert(cleanIndex >= 0 && createIndex > cleanIndex,
        "Bundle构建没有在复制新文件前清理整个Contents，删除或改名的旧依赖可能残留。");
    Assert(project.Contains("Condition=\"'$(BuildBundle)' == 'true'\"", StringComparison.Ordinal),
        "普通开发构建仍会清空并覆盖正式多版本Bundle，必须改为显式启用BuildBundle。") ;
}

static void AutoCadVersionManifestUsesExactLanes()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var manifestPath = Path.Combine(root, "packaging", "YiZhiMaoDc.bundle", "PackageContents.xml");
    var matrixPath = Path.Combine(root, "tests", "autocad-version-matrix.ps1");
    var bundleBuildPath = Path.Combine(root, "packaging", "build-multiversion-bundle.ps1");
    var document = XDocument.Load(manifestPath);
    var components = document.Root?.Elements("Components").ToList() ?? [];
    var expected = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["R21.0"] = "./Contents/R21.0/YiZhiMao.Dc.Plugin.dll",
        ["R22.0"] = "./Contents/R22.0/YiZhiMao.Dc.Plugin.dll",
        ["R23.0"] = "./Contents/R23.0/YiZhiMao.Dc.Plugin.dll",
        ["R23.1"] = "./Contents/R23.1/YiZhiMao.Dc.Plugin.dll",
        ["R24.0"] = "./Contents/R24.0/YiZhiMao.Dc.Plugin.dll",
        ["R24.1"] = "./Contents/R24.1/YiZhiMao.Dc.Plugin.dll",
        ["R24.2"] = "./Contents/R24.2/YiZhiMao.Dc.Plugin.dll",
        ["R24.3"] = "./Contents/R24.3/YiZhiMao.Dc.Plugin.dll",
        ["R25.0"] = "./Contents/R25.0/YiZhiMao.Dc.Plugin.dll",
        ["R25.1"] = "./Contents/R25.1/YiZhiMao.Dc.Plugin.dll",
        ["R26.0"] = "./Contents/R26.0/YiZhiMao.Dc.Plugin.dll"
    };

    Assert(components.Count == expected.Count, "PackageContents没有为十一个AutoCAD版本建立独立组件。");
    foreach (var component in components)
    {
        var requirements = component.Element("RuntimeRequirements");
        var entry = component.Element("ComponentEntry");
        var minimum = requirements?.Attribute("SeriesMin")?.Value;
        var maximum = requirements?.Attribute("SeriesMax")?.Value;
        var platform = requirements?.Attribute("Platform")?.Value;
        var module = entry?.Attribute("ModuleName")?.Value;
        Assert(platform == "AutoCAD" && minimum is not null && minimum == maximum &&
               expected.TryGetValue(minimum, out var expectedModule) && module == expectedModule,
            $"AutoCAD版本分流无效：{minimum ?? "缺少SeriesMin"}。");
    }

    var matrix = File.ReadAllText(matrixPath);
    var bundleBuild = File.ReadAllText(bundleBuildPath);
    Assert(matrix.Contains("[System.Reflection.AssemblyName]::GetAssemblyName", StringComparison.Ordinal) &&
           bundleBuild.Contains("[System.Reflection.AssemblyName]::GetAssemblyName", StringComparison.Ordinal),
        "版本矩阵或正式Bundle构建没有核对SDK程序集版本，错误SDK可能被装入版本通道。") ;
}

static void PrintSettingsPersistAndValidate()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var path = Path.Combine(root, "PrintSettings.json");
        var store = new PrintSettingsStore(path);
        var settings = new PrintSettings
        {
            PlotTransparency = true,
            PdfOutputMode = PdfOutputMode.SeparateAndMerged,
            ImageDpi = 600,
            Margin = 3.5,
            Paper = "ISO_A3"
        };

        store.Save(settings);
        var loaded = store.Load();
        Assert(loaded.PlotTransparency, "打印透明度未持久化。");
        Assert(loaded.PdfOutputMode == PdfOutputMode.SeparateAndMerged, "PDF输出模式未持久化。");
        Assert(loaded.ImageDpi == 600 && Math.Abs(loaded.Margin - 3.5) < 0.000001, "打印数值设置未持久化。");
        Assert(loaded.Paper == "ISO_A3", "纸张设置未持久化。");

        loaded.ImageDpi = 0;
        AssertThrows<InvalidOperationException>(() => store.Save(loaded), "非法分辨率未被拒绝。");
        loaded.ImageDpi = 200;
        loaded.PdfOutputMode = (PdfOutputMode)99;
        AssertThrows<InvalidOperationException>(() => store.Save(loaded), "非法PDF输出模式未被拒绝。");
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void PrintFramesAreCleanedAndSorted()
{
    var frames = new[]
    {
        new PrintFrame(110, 100, 210, 160, "右上"),
        new PrintFrame(0, 0, 100, 60, "左下"),
        new PrintFrame(0, 100, 100, 160, "左上"),
        new PrintFrame(10, 110, 90, 150, "嵌套"),
        new PrintFrame(0, 100, 100, 160, "重复"),
        new PrintFrame(120, 0, 220, 60, "右下")
    };

    var prepared = PrintFrameProcessor.Prepare(frames);
    Assert(prepared.Select(frame => frame.SourceKey).SequenceEqual(["左上", "右上", "左下", "右下"]),
        "图框未按从上到下、从左到右排序，或未正确去重/去嵌套。");
}

static void PrintOptionsChooseOnlyAvailableValues()
{
    var available = new[] { "ISO_A4", "ISO_A3" };

    Assert(PrintOptionSelector.SelectRequired(["旧设备纸张", "ISO_A3"], available, "纸张") == "ISO_A3",
        "旧设备纸张未回退到当前设备支持的纸张。") ;
    Assert(PrintOptionSelector.SelectRequired(["iso_a4"], available, "纸张") == "ISO_A4",
        "打印选项匹配未忽略大小写。") ;
    AssertThrows<InvalidOperationException>(
        () => PrintOptionSelector.SelectRequired(["不存在"], Array.Empty<string>(), "纸张"),
        "空设备选项列表未返回明确错误。") ;
    Assert(PrintOptionSelector.IsPlotStyleCompatible("monochrome.ctb", usesColorDependentStyles: true),
        "CTB样式被错误拒绝。") ;
    Assert(!PrintOptionSelector.IsPlotStyleCompatible("monochrome.ctb", usesColorDependentStyles: false),
        "命名样式图纸错误接受了CTB。") ;
}

static void PrintCopyOrderMatchesCollateSetting()
{
    var collated = PrintCopyPlanner.Build([2, 2], collate: true);
    Assert(collated.SequenceEqual([
            new PrintCopyStep(0, 0),
            new PrintCopyStep(1, 0),
            new PrintCopyStep(0, 1),
            new PrintCopyStep(1, 1)
        ]),
        "逐份打印没有按第1份A/B、第2份A/B的顺序执行。");

    var uncollated = PrintCopyPlanner.Build([2, 2], collate: false);
    Assert(uncollated.SequenceEqual([
            new PrintCopyStep(0, 0),
            new PrintCopyStep(0, 1),
            new PrintCopyStep(1, 0),
            new PrintCopyStep(1, 1)
        ]),
        "非逐份打印没有按A/A、B/B的顺序执行。");

    var uneven = PrintCopyPlanner.Build([2, 1], collate: true);
    Assert(uneven.SequenceEqual([
            new PrintCopyStep(0, 0),
            new PrintCopyStep(1, 0),
            new PrintCopyStep(0, 1)
        ]),
        "不同份数任务的逐份顺序不正确。");
}

static void PdfToolScansAndMergesInOrder()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var first = Path.Combine(root, "A.pdf");
        var second = Path.Combine(root, "B.pdf");
        var broken = Path.Combine(root, "broken.pdf");
        CreatePdf(first, 1);
        CreatePdf(second, 2);
        File.WriteAllText(broken, "not a pdf");

        var scanResult = PdfToolService.ScanWithDiagnostics(root);
        var scanned = scanResult.Files;
        Assert(scanned.Count == 2 && scanResult.Failures.Count == 1, "损坏PDF没有被单独跳过并报告。");
        Assert(scanned[0].FileName == "A.pdf" && scanned[0].PageCount == 1, "PDF扫描排序或页数不正确。");
        Assert(scanned[1].FileName == "B.pdf" && scanned[1].PageCount == 2, "PDF扫描页数不正确。");
        Assert(Path.GetFileName(scanResult.Failures[0].Path) == "broken.pdf", "PDF扫描失败项不正确。");

        var merged = Path.Combine(root, "merged.pdf");
        File.WriteAllText(merged, "existing output");
        var pageCount = PdfToolService.Merge([second, first], merged);
        Assert(pageCount == 3 && PdfToolService.GetPageCount(merged) == 3, "PDF未按输入列表完整合并。");
        AssertThrows<InvalidOperationException>(() => PdfToolService.Merge([first], first), "未阻止输出覆盖输入PDF。");
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void BatchPrintSurfaceContractIsComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "BatchPrintCommands.cs");
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var printWindowPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "BatchPrintWindow.xaml");
    var pdfWindowPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "PdfToolWindow.xaml");
    var projectPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "YiZhiMao.Dc.Plugin.csproj");
    var resolverPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginDependencyResolver.cs");
    var applicationPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs");
    Assert(File.Exists(commandsPath) && File.Exists(panelPath) && File.Exists(printWindowPath) &&
           File.Exists(pdfWindowPath) && File.Exists(resolverPath),
        "批量打印或PDF工具界面文件缺失。");

    var commands = File.ReadAllText(commandsPath);
    foreach (var command in new[]
             {
                 "DC_CMD_BATCH_PRINT",
                 "DC_CMD_PRINT_PAPER",
                 "DC_CMD_PRINT_PDF",
                 "DC_CMD_PRINT_LAYOUT_PAPER",
                 "DC_CMD_PRINT_LAYOUT_PDF",
                 "DC_CMD_DRAW_PRINT_FRAME",
                 "DC_CMD_PDF_TOOL"
             })
    {
        Assert(commands.Contains($"[CommandMethod(\"{command}\"", StringComparison.Ordinal), $"缺少底层命令 {command}。");
    }

    Assert(commands.Contains("\"DCD1\")", StringComparison.Ordinal) &&
           commands.Contains("\"DCD2\")", StringComparison.Ordinal) &&
           commands.Contains("\"DCD3\")", StringComparison.Ordinal) &&
           commands.Contains("\"DCD4\")", StringComparison.Ordinal) &&
           commands.Contains("\"DCDY\")", StringComparison.Ordinal), "DCD1-DCD4 或 DCDY 默认快捷键缺失。");

    var panel = File.ReadAllText(panelPath);
    Assert(panel.Contains("Text=\"批量打印\"", StringComparison.Ordinal), "左侧未增加批量打印模块。");
    Assert(panel.Contains("Content=\"PDF 工具\"", StringComparison.Ordinal), "批量打印面板缺少PDF工具入口。");
    Assert(panel.Contains("Content=\"闭合矩形多段线\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"四条直线组成的矩形\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"块参照外框\"", StringComparison.Ordinal), "图框识别选项不完整。");
    Assert(panel.Contains("x:Name=\"ImageDpiSlider\"", StringComparison.Ordinal) &&
           panel.Contains("着色/光栅分辨率", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"VectorDpiSlider\"", StringComparison.Ordinal) &&
           !panel.Contains("x:Name=\"GradientDpiSlider\"", StringComparison.Ordinal),
        "分辨率界面仍暴露AutoCAD托管API无法实际应用的矢量或渐变DPI。") ;

    var printWindow = File.ReadAllText(printWindowPath);
    Assert(printWindow.Contains("Header=\"预览\"", StringComparison.Ordinal) &&
           printWindow.Contains("Header=\"删除\"", StringComparison.Ordinal) &&
           printWindow.Contains("Content=\"应用到选中\"", StringComparison.Ordinal), "批量打印中心任务操作不完整。");

    var pdfWindow = File.ReadAllText(pdfWindowPath);
    Assert(pdfWindow.Contains("Content=\"全选\"", StringComparison.Ordinal) &&
           pdfWindow.Contains("Content=\"反选\"", StringComparison.Ordinal) &&
           pdfWindow.Contains("Content=\"开始合并所选文件\"", StringComparison.Ordinal), "PDF工具选择或合并入口不完整。");

    var project = File.ReadAllText(projectPath);
    Assert(project.Contains("<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>", StringComparison.Ordinal) &&
           project.Contains("<BundleAssemblies Include=\"$(TargetDir)*.dll\"", StringComparison.Ordinal) &&
           project.Contains("<BundleDependencyMetadata Include=\"$(TargetDir)*.deps.json\"", StringComparison.Ordinal),
        "PDFsharp运行依赖或.NET依赖解析清单未纳入bundle复制规则。");
    var resolver = File.ReadAllText(resolverPath);
    var application = File.ReadAllText(applicationPath);
    Assert(resolver.Contains("AssemblyLoadContext.GetLoadContext", StringComparison.Ordinal) &&
           resolver.Contains("Resolving += ResolveCoreAssembly", StringComparison.Ordinal) &&
           application.Contains("PluginDependencyResolver.Initialize()", StringComparison.Ordinal) &&
           application.Contains("PluginDependencyResolver.Dispose()", StringComparison.Ordinal),
        "AutoCAD插件加载上下文没有从插件目录解析PDFsharp传递依赖。");
}

static void BatchPrintRuntimeRegressionContract()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var plotServicePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Printing", "CadPlotService.cs");
    var plotService = File.ReadAllText(plotServicePath);

    var recognitionPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Printing", "FrameRecognitionService.cs");
    var recognition = File.ReadAllText(recognitionPath);
    Assert(!recognition.Contains("layoutDictionary", StringComparison.Ordinal) &&
           !recognition.Contains(".Cast<DBDictionaryEntry>()", StringComparison.Ordinal),
        "当前布局打印仍在枚举布局字典，可能重新引入AutoCAD 2026类型转换错误。") ;
    Assert(plotService.Contains("PrintOptionSelector.SelectRequired", StringComparison.Ordinal),
        "打印纸张未经过设备支持列表解析。") ;
    Assert(plotService.Contains("SetPlotConfigurationName(plotSettings, device, paper)", StringComparison.Ordinal) &&
           plotService.Contains("RefreshLists(plotSettings)", StringComparison.Ordinal),
        "正式打印设置未绑定已解析的设备与纸张并刷新列表。") ;
    Assert(plotService.Contains("当前阶段", StringComparison.Ordinal),
        "打印异常未包含失败阶段，无法继续定位AutoCAD错误。") ;
    Assert(plotService.Contains("finally", StringComparison.Ordinal) &&
           plotService.Contains("TryCompletePlotStage(() => engine.EndGenerateGraphics(null))", StringComparison.Ordinal) &&
           plotService.Contains("TryCompletePlotStage(() => engine.EndPage(null))", StringComparison.Ordinal) &&
           plotService.Contains("TryCompletePlotStage(() => engine.EndDocument(null))", StringComparison.Ordinal) &&
           plotService.Contains("TryCompletePlotStage(() => engine.EndPlot(null))", StringComparison.Ordinal),
        "打印取消或异常时没有成对释放原生打印阶段，可能使AutoCAD停留在打印状态。") ;
    Assert(plotService.Contains("PrintCopyPlanner.Build", StringComparison.Ordinal) &&
           plotService.Contains("temporarySinglesDirectory", StringComparison.Ordinal) &&
           plotService.Contains("TryDeleteDirectory(temporarySinglesDirectory)", StringComparison.Ordinal),
        "逐份顺序或仅合并模式的临时单页清理没有接入正式打印流程。") ;
}

static void BatchPrintNarrowPanelAndWindowAreaContract()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var plotServicePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Printing", "CadPlotService.cs");

    var panelDocument = XDocument.Load(panelPath);
    var batchView = panelDocument.Descendants()
        .Single(element => element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" && attribute.Value == "BatchPrintModuleView"));
    Assert((string?)batchView.Attribute("HorizontalScrollBarVisibility") == "Disabled",
        "批量打印页仍允许横向滚动，窄面板会遮挡右列按钮。");
    var batchContainer = batchView.Elements().Single();
    Assert(batchContainer.Attribute("MinWidth") is null,
        "批量打印内容仍设置最小宽度，窄面板无法自适应。");

    var commandButtons = panelDocument.Descendants()
        .Where(element => element.Name.LocalName == "Button" &&
                          ((string?)element.Attribute("Tag"))?.StartsWith("DC_CMD_PRINT", StringComparison.Ordinal) == true ||
                          element.Name.LocalName == "Button" &&
                          (string?)element.Attribute("Tag") is "DC_CMD_DRAW_PRINT_FRAME" or "DC_CMD_PDF_TOOL")
        .ToList();
    Assert(commandButtons.Count == 6 && commandButtons.All(button =>
               (string?)button.Attribute("Height") == "32" &&
               (string?)button.Attribute("FontSize") == "11"),
        "六个快捷出图按钮尚未按窄面板尺寸统一缩放。");
    var saveButton = panelDocument.Descendants()
        .Single(element => (string?)element.Attribute("Click") == "OnSavePrintSettingsClick");
    Assert((string?)saveButton.Attribute("Height") == "32" &&
           (string?)saveButton.Attribute("FontSize") == "11",
        "保存批量打印设置按钮尚未按窄面板尺寸缩放。");
    var pdfOutputModeCombo = panelDocument.Descendants()
        .Single(element => (string?)element.Attributes().FirstOrDefault(attribute =>
            attribute.Name.LocalName == "Name") == "PdfOutputModeCombo");
    Assert((string?)pdfOutputModeCombo.Attribute("VerticalContentAlignment") == "Center",
        "PDF 输出模式下拉框文字没有垂直居中。");

    var plotService = File.ReadAllText(plotServicePath);
    Assert(plotService.Contains("var extents = new Extents3d(", StringComparison.Ordinal) &&
           plotService.Contains("extents.TransformBy(wcsToDcs);", StringComparison.Ordinal),
        "打印窗口没有整体转换Extents3d，旋转视图下DCS范围可能无效。");
    foreach (var stage in new[]
             {
                 "设置窗口打印类型", "转换打印窗口范围", "设置适合图纸缩放", "设置打印居中", "设置打印方向"
             })
    {
        Assert(plotService.Contains($"currentStage = \"{stage}\"", StringComparison.Ordinal),
            $"缺少打印区域子阶段：{stage}。");
    }
}

static void BatchPrintUsesLayoutBoundMediaAndWindowSequence()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var plotServicePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Printing", "CadPlotService.cs");
    var printWindowPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "BatchPrintWindow.xaml.cs");
    var plotService = File.ReadAllText(plotServicePath);
    var printWindow = File.ReadAllText(printWindowPath);

    Assert(plotService.Contains("GetMedia(Document document, string device)", StringComparison.Ordinal) &&
           printWindow.Contains("CadPlotService.GetMedia(document, PrinterCombo.Text)", StringComparison.Ordinal),
        "纸张列表仍脱离当前图纸布局读取。");
    Assert(plotService.Contains("plotSettings.CopyFrom(layout);", StringComparison.Ordinal) &&
           plotService.Contains("SetPlotConfigurationName(plotSettings, device, null)", StringComparison.Ordinal) &&
           plotService.Contains("GetCanonicalMediaNameList(plotSettings)", StringComparison.Ordinal),
        "纸张列表未从复制当前布局并绑定设备后的PlotSettings读取。");

    var windowAreaCall = plotService.IndexOf(
        "SetPlotWindowArea(plotSettings, plotWindow)", StringComparison.Ordinal);
    var windowTypeCall = plotService.IndexOf(
        "SetPlotType(plotSettings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window)",
        windowAreaCall + 1,
        StringComparison.Ordinal);
    Assert(windowAreaCall >= 0 && windowTypeCall > windowAreaCall,
        "打印窗口仍在写入范围前切换为Window，AutoCAD 2026会返回InvalidInput。");
}

static void BatchPrintSeparatesPaperDevicesAndDisablesBackgroundPlot()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var plotServicePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Printing", "CadPlotService.cs");
    var printWindowPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "BatchPrintWindow.xaml.cs");
    var plotService = File.ReadAllText(plotServicePath);
    var printWindow = File.ReadAllText(printWindowPath);

    Assert(plotService.Contains("GetPaperDevices()", StringComparison.Ordinal) &&
           plotService.Contains("PlotToFileCapability.MustPlotToFile", StringComparison.Ordinal),
        "D1尚未排除只能输出文件的打印设备。");
    Assert(printWindow.Contains("toPdf ? CadPlotService.GetPdfDevices() : CadPlotService.GetPaperDevices()",
            StringComparison.Ordinal),
        "批量打印窗口没有按D1/D2用途分离设备列表。");
    Assert(plotService.Contains("GetPdfDevices()", StringComparison.Ordinal) &&
           plotService.Contains("PlotToFileCapability.MustPlotToFile", StringComparison.Ordinal),
        "PDF输出仍可能列出不能输出PDF的设备。");
    Assert(plotService.Contains("GetSystemVariable(\"BACKGROUNDPLOT\")", StringComparison.Ordinal) &&
           plotService.Contains("SetSystemVariable(\"BACKGROUNDPLOT\", 0)", StringComparison.Ordinal) &&
           plotService.Contains("SetSystemVariable(\"BACKGROUNDPLOT\", previousBackgroundPlot)",
               StringComparison.Ordinal),
        "批量任务没有同步关闭并恢复AutoCAD后台打印。");
}

static void BatchPrintTargetsCurrentLayoutAndChecksPaperPrerequisites()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var commandsPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "BatchPrintCommands.cs");
    var recognitionPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Printing", "FrameRecognitionService.cs");
    var plotServicePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Printing", "CadPlotService.cs");
    var panelPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");

    var commands = File.ReadAllText(commandsPath);
    var recognition = File.ReadAllText(recognitionPath);
    var plotService = File.ReadAllText(plotServicePath);
    var panel = File.ReadAllText(panelPath);

    Assert(commands.Contains("FrameRecognitionService.GetCurrentOrActivateFirstPaperSpaceLayout(document", StringComparison.Ordinal) &&
           !commands.Contains("请先切换到要打印的布局", StringComparison.Ordinal),
        "D3/D4在模型空间仍然要求人工切换布局。");
    Assert(recognition.Contains("GetCurrentOrActivateFirstPaperSpaceLayout", StringComparison.Ordinal) &&
           recognition.Contains("GetFirstPaperSpaceLayoutName", StringComparison.Ordinal) &&
           recognition.Contains("LayoutManager.Current.CurrentLayout = firstLayoutName", StringComparison.Ordinal) &&
           recognition.Contains("document.Editor.Regen()", StringComparison.Ordinal),
        "D3/D4没有激活并显示首个非Model布局。");
    Assert(recognition.Contains("GetCurrentPaperSpaceLayout", StringComparison.Ordinal) &&
           recognition.Contains("database.CurrentSpaceId", StringComparison.Ordinal) &&
           recognition.Contains("layout.ModelType", StringComparison.Ordinal),
        "布局切换后没有重新从当前空间解析打印任务。");
    Assert(panel.Contains("Content=\"DCD3 布局打印纸张\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"DCD4 布局打印 PDF\"", StringComparison.Ordinal) &&
           !panel.Contains("全布局打印", StringComparison.Ordinal),
        "D3/D4按钮仍显示为全布局打印。");
    Assert(plotService.Contains("EnsurePaperPrintingAvailable", StringComparison.Ordinal) &&
           plotService.Contains("Windows 打印后台处理程序已被禁用", StringComparison.Ordinal),
        "D1没有在进入纸张打印前检查并说明Windows打印服务前置条件。");
}

static void BatchPrintPreviewGeneratesAndOpensPdf()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var plotServicePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Printing", "CadPlotService.cs");
    var printWindowPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "BatchPrintWindow.xaml.cs");
    var plotService = File.ReadAllText(plotServicePath);
    var printWindow = File.ReadAllText(printWindowPath);

    Assert(printWindow.Contains("CreatePreview([job])", StringComparison.Ordinal) &&
           printWindow.Contains("CreatePreview(selectedJobs)", StringComparison.Ordinal) &&
           printWindow.Contains("CadPlotService.CreatePreview(", StringComparison.Ordinal) &&
           !printWindow.Contains("CadPlotService.ZoomTo", StringComparison.Ordinal),
        "单项预览或全部预览仍只缩放CAD视图，没有生成PDF文件。");
    Assert(plotService.Contains("public static string CreatePreview(", StringComparison.Ordinal) &&
           plotService.Contains("DWG To PDF.pc3", StringComparison.Ordinal) &&
           plotService.Contains("GetPdfDevices()", StringComparison.Ordinal) &&
           plotService.Contains("PdfOutputMode.MergedOnly", StringComparison.Ordinal) &&
           plotService.Contains("Path.GetTempPath()", StringComparison.Ordinal) &&
           plotService.Contains("CleanupOldPreviewDirectories", StringComparison.Ordinal) &&
           plotService.Contains("TryOpen(previewPath)", StringComparison.Ordinal),
        "预览服务没有固定生成临时合并PDF并自动打开。");

    var startHandler = printWindow.IndexOf("private void OnStartClick", StringComparison.Ordinal);
    var paperCheck = printWindow.IndexOf("CadPlotService.EnsurePaperPrintingAvailable()", StringComparison.Ordinal);
    Assert(startHandler >= 0 && paperCheck > startHandler,
        "D1仍在窗口初始化时检查Print Spooler，会阻断PDF预览。");
}

static void LineRectangleDetectorUsesToleranceIndexWithoutDuplicates()
{
    var segments = new[]
    {
        new AxisAlignedSegment(0, 0, 100, 0),
        new AxisAlignedSegment(100.0004, 50, 0.0004, 50.0002),
        new AxisAlignedSegment(0.0002, -0.0002, 0.0004, 50.0003),
        new AxisAlignedSegment(99.9998, 50.0002, 100.0002, -0.0003),
        new AxisAlignedSegment(0, 0, 100, 50)
    };

    var matches = AxisAlignedRectangleDetector.Find(segments);

    Assert(matches.Count == 1, "一个四边散线图框未被唯一识别。") ;
    var match = matches[0];
    Assert(match.BottomIndex == 0 && match.TopIndex == 1 && match.LeftIndex == 2 && match.RightIndex == 3,
        "散线图框边界索引识别错误。") ;
    Assert(Math.Abs(match.MinX) < 0.001 && Math.Abs(match.MaxY - 50.0001) < 0.001,
        "散线图框没有保留容差内的实际边界。") ;
    AssertThrows<ArgumentOutOfRangeException>(
        () => AxisAlignedRectangleDetector.Find(segments, 0),
        "散线图框检测未拒绝无效容差。") ;
}

static void SecurityDependencyAndPanelTimerAreHardened()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var projectPath = Path.Combine(root, "src", "YiZhiMao.Dc.Core", "YiZhiMao.Dc.Core.csproj");
    var project = XDocument.Load(projectPath);
    var pkcsReference = project.Descendants("PackageReference")
        .SingleOrDefault(element => (string?)element.Attribute("Include") == "System.Security.Cryptography.Pkcs");
    Assert((string?)pkcsReference?.Attribute("Version") == "8.0.2",
        "System.Security.Cryptography.Pkcs 未固定到 .NET 8 已修复版本 8.0.2。") ;

    var panelCode = File.ReadAllText(Path.Combine(
        root,
        "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs"));
    var unloadStart = panelCode.IndexOf("private void OnShortcutPanelViewUnloaded", StringComparison.Ordinal);
    var timerStop = panelCode.IndexOf("statusClearTimer.Stop();", unloadStart, StringComparison.Ordinal);
    var nextHandler = panelCode.IndexOf("private void OnShortcutPanelViewLoaded", unloadStart, StringComparison.Ordinal);
    Assert(unloadStart >= 0 && timerStop > unloadStart && timerStop < nextHandler,
        "快捷键面板卸载时未停止状态计时器。") ;
}

static void BenchmarkLineRectangleDetector()
{
    const int frameCount = 5000;
    var segments = new List<AxisAlignedSegment>(frameCount * 4);
    for (var index = 0; index < frameCount; index++)
    {
        var x = index * 1000d;
        segments.Add(new AxisAlignedSegment(x, 0, x + 800, 0));
        segments.Add(new AxisAlignedSegment(x, 600, x + 800, 600));
        segments.Add(new AxisAlignedSegment(x, 0, x, 600));
        segments.Add(new AxisAlignedSegment(x + 800, 0, x + 800, 600));
    }

    _ = AxisAlignedRectangleDetector.Find(segments);
    for (var run = 1; run <= 3; run++)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var matches = AxisAlignedRectangleDetector.Find(segments);
        stopwatch.Stop();
        Console.WriteLine($"图框索引基准 {run}/3：{frameCount} 个图框，{matches.Count} 个结果，{stopwatch.Elapsed.TotalMilliseconds:0.###} ms");
    }
}

static void CreatePdf(string path, int pageCount)
{
    using var document = new PdfSharp.Pdf.PdfDocument();
    for (var index = 0; index < pageCount; index++)
    {
        document.AddPage();
    }

    document.Save(path);
}

static void ShortcutPanelStaticResourcesAreDefined()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var xamlPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml");
    var themePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Themes", "ToolboxTheme.xaml");
    var xaml = File.ReadAllText(xamlPath);
    var theme = File.ReadAllText(themePath);
    var document = XDocument.Parse(xaml);
    var themeDocument = XDocument.Parse(theme);
    var resourceKeys = document.Descendants()
        .Attributes()
        .Where(attribute => attribute.Name.LocalName == "Key")
        .Select(attribute => attribute.Value)
        .Concat(themeDocument.Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName == "Key")
            .Select(attribute => attribute.Value))
        .ToHashSet(StringComparer.Ordinal);
    var references = Regex.Matches(xaml + theme, @"\{StaticResource\s+([^}\s]+)")
        .Select(match => match.Groups[1].Value)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    var undefined = references.Where(reference => !resourceKeys.Contains(reference)).ToArray();

    Assert(xaml.Contains("Themes/ToolboxTheme.xaml", StringComparison.Ordinal),
        "工具箱面板没有合并全局主题资源字典。" );
    Assert(undefined.Length == 0,
        $"工具箱XAML引用了未定义资源：{string.Join(", ", undefined)}。" );

    var requiredControlStyles = new[]
    {
        "DcTextBoxStyle",
        "DcComboBoxStyle",
        "DcComboBoxItemStyle",
        "DcCheckBoxStyle",
        "DcRadioButtonStyle",
        "DcListBoxStyle",
        "DcListBoxItemStyle",
        "DcExpanderStyle",
        "DcSliderStyle",
        "DcScrollBarStyle",
        "DcDataGridColumnHeaderStyle"
    };
    var missingControlStyles = requiredControlStyles
        .Where(style => !resourceKeys.Contains(style))
        .ToArray();
    Assert(missingControlStyles.Length == 0,
        $"全局主题缺少控件样式：{string.Join(", ", missingControlStyles)}。" );
    Assert(theme.Contains("Stroke=\"White\"", StringComparison.Ordinal) &&
           theme.Contains("ThemeAccentSoftBrush", StringComparison.Ordinal) &&
           theme.Contains("TargetType=\"ComboBoxItem\"", StringComparison.Ordinal),
        "勾选、选中和下拉列表没有统一使用橙灰主题状态。" );
    Assert(theme.Contains("x:Name=\"FocusBorder\"", StringComparison.Ordinal) &&
           theme.Contains("VerticalAlignment=\"{TemplateBinding VerticalContentAlignment}\"", StringComparison.Ordinal) &&
           !theme.Contains("TargetName=\"InputBorder\" Property=\"BorderThickness\" Value=\"1.5\"", StringComparison.Ordinal) &&
           theme.Contains("<Setter Property=\"Padding\" Value=\"7,0\" />", StringComparison.Ordinal),
        "输入框焦点状态会改变布局尺寸，或紧凑输入框没有稳定垂直居中。" );
    Assert(xaml.Contains("x:Key=\"MutedGridTextBlockStyle\"", StringComparison.Ordinal) &&
           xaml.Contains("ElementStyle=\"{StaticResource MutedGridTextBlockStyle}\"", StringComparison.Ordinal),
        "底层命令列没有使用固定灰色只读文字样式。" );

    XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    static bool HasVerticalPadding(string value)
    {
        var parts = value.Split(',')
            .Select(part => double.Parse(part.Trim(), CultureInfo.InvariantCulture))
            .ToArray();
        return parts.Length switch
        {
            1 => parts[0] != 0,
            2 => parts[1] != 0,
            4 => parts[1] != 0 || parts[3] != 0,
            _ => true
        };
    }

    var verticallyPaddedTextBoxStyles = document.Descendants(presentation + "Style")
        .Concat(themeDocument.Descendants(presentation + "Style"))
        .Where(style => string.Equals((string?)style.Attribute("TargetType"), "TextBox", StringComparison.Ordinal))
        .SelectMany(style => style.Elements(presentation + "Setter"))
        .Where(setter => string.Equals((string?)setter.Attribute("Property"), "Padding", StringComparison.Ordinal))
        .Select(setter => (string?)setter.Attribute("Value"))
        .Where(value => !string.IsNullOrWhiteSpace(value) && HasVerticalPadding(value))
        .ToArray();
    Assert(verticallyPaddedTextBoxStyles.Length == 0,
        $"输入框样式仍含上下内边距，会与垂直居中叠加并导致文字偏移或裁切：{string.Join(", ", verticallyPaddedTextBoxStyles)}");
    var hardwareView = document.Descendants(presentation + "Grid")
        .Single(element => string.Equals(
            (string?)element.Attribute(xamlNamespace + "Name"),
            "HardwareModuleView",
            StringComparison.Ordinal));
    var hardwareHingeInputs = hardwareView.Descendants(presentation + "TextBox")
        .Where(element => ((string?)element.Attribute(xamlNamespace + "Name"))
            ?.StartsWith("HardwareHinge", StringComparison.Ordinal) == true)
        .ToArray();
    Assert(hardwareHingeInputs.Length > 0 &&
           hardwareHingeInputs.All(element =>
               string.Equals(
                   (string?)element.Attribute("Style"),
                   "{StaticResource HardwareInputStyle}",
                   StringComparison.Ordinal) &&
               element.Attribute("Height") is null),
        "五金铰链输入框仍混用带上下内边距的表单样式和更小固定高度，会裁切文字。" );

    Assert(resourceKeys.Contains("DcDataGridTextBlockStyle") &&
           theme.Contains("x:Key=\"DcDataGridTextBlockStyle\"", StringComparison.Ordinal) &&
           theme.Contains("<Setter Property=\"Margin\" Value=\"0,2,0,0\" />", StringComparison.Ordinal),
        "全局表格显示文字样式没有稳定垂直居中和基线补偿。" );

    var viewsRoot = Path.GetDirectoryName(xamlPath)!;
    foreach (var fileName in new[] { "BatchPrintWindow.xaml", "PdfToolWindow.xaml", "HingeTypeMenuWindow.xaml" })
    {
        var toolWindow = File.ReadAllText(Path.Combine(viewsRoot, fileName));
        Assert(toolWindow.Contains("Themes/ToolboxTheme.xaml", StringComparison.Ordinal),
            $"{fileName} 没有合并全局主题资源字典。" );
    }

    var tableFiles = new[] { "ShortcutPanelView.xaml", "BatchPrintWindow.xaml", "PdfToolWindow.xaml" };
    var unalignedColumns = tableFiles
        .Select(fileName => new
        {
            FileName = fileName,
            Document = XDocument.Parse(File.ReadAllText(Path.Combine(viewsRoot, fileName)))
        })
        .SelectMany(item => item.Document.Descendants(presentation + "DataGridTextColumn")
            .Where(column => column.Attribute("ElementStyle") is null &&
                             !column.Elements(presentation + "DataGridTextColumn.ElementStyle").Any())
            .Select(column => $"{item.FileName}:{(string?)column.Attribute("Header") ?? "(无标题)"}"))
        .ToArray();
    Assert(unalignedColumns.Length == 0,
        $"表格列没有显式使用垂直居中的显示文字样式：{string.Join(", ", unalignedColumns)}。" );

    var layerManager = File.ReadAllText(Path.Combine(viewsRoot, "LayerManagerWindow.cs"));
    var radialWindow = File.ReadAllText(Path.Combine(viewsRoot, "RadialMenuWindow.cs"));
    var radialControl = File.ReadAllText(Path.Combine(viewsRoot, "RadialMenuControl.cs"));
    Assert(layerManager.Contains("Themes/ToolboxTheme.xaml", StringComparison.Ordinal),
        "图层管理器没有加载全局主题资源。" );
    Assert(radialWindow.Contains("Themes/ToolboxTheme.xaml", StringComparison.Ordinal) &&
           radialControl.Contains("ThemeAccentBrush", StringComparison.Ordinal),
        "圆盘没有使用全局橙色交互高亮。" );
}

static void ShortcutPanelLayoutKeepsNavigationAndOverflowContract()
{
    var xamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    Assert(File.Exists(xamlPath), "未找到快捷键面板XAML，无法检查布局约束。") ;
    var shortcutManagerPath = Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(xamlPath)!,
        "..", "Shortcuts", "ShortcutManager.cs"));
    Assert(File.Exists(shortcutManagerPath), "未找到快捷键管理器，无法检查默认快捷键保存规则。") ;
    var imageCommandsPath = Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(xamlPath)!,
        "..", "ImageBatchCommands.cs"));
    Assert(File.Exists(imageCommandsPath), "未找到批量图片插入命令。") ;

    var xaml = File.ReadAllText(xamlPath);
    var theme = File.ReadAllText(Path.Combine(
        Path.GetDirectoryName(xamlPath)!, "..", "Themes", "ToolboxTheme.xaml"));
    var xamlDocument = XDocument.Parse(xaml);
    var searchBoxes = xamlDocument.Descendants()
        .Where(element => element.Name.LocalName == "TextBox" &&
                          element.Attributes().Any(attribute =>
                              attribute.Name.LocalName == "Name" &&
                              (attribute.Value == "SearchBox" || attribute.Value == "CadSearchBox")))
        .ToArray();
    Assert(searchBoxes.Length == 2 && searchBoxes.All(searchBox =>
        searchBox.Attribute("Width") is null &&
        searchBox.Attribute("MaxWidth") is null &&
        searchBox.Parent?.Attribute("Width") is null &&
        searchBox.Parent?.Attribute("MaxWidth") is null &&
        ((string?)searchBox.Parent?.Attribute("HorizontalAlignment") is null or "Stretch")),
        "两个命令搜索框没有随内容区横向拉伸，仍受固定宽度或最大宽度限制。");
    static XElement FindNamedDataGrid(XDocument document, string name)
    {
        return document.Descendants()
            .Single(element => element.Name.LocalName == "DataGrid" &&
                               element.Attributes().Any(attribute =>
                                   attribute.Name.LocalName == "Name" &&
                                   string.Equals(attribute.Value, name, StringComparison.Ordinal)));
    }

    static XElement[] GetDataGridColumns(XElement dataGrid)
    {
        return dataGrid.Descendants()
            .Where(element => element.Parent?.Name.LocalName == "DataGrid.Columns" &&
                              element.Name.LocalName.EndsWith("Column", StringComparison.Ordinal))
            .ToArray();
    }

    static bool HasReadableMinimumWidth(XElement column)
    {
        return double.TryParse(
                   (string?)column.Attribute("MinWidth"),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out var minimumWidth) &&
               minimumWidth >= 48;
    }

    var expectedModuleOrder = new[]
    {
        "PreferencesModuleButton",
        "BoardModuleButton",
        "DoorPanelModuleButton",
        "HardwareModuleButton",
        "WallPanelModuleButton",
        "ImageModuleButton",
        "OneClickFrameModuleButton",
        "BatchPrintModuleButton",
        "StatisticsModuleButton",
        "DrawingLayerModuleButton",
        "RadialMenuModuleButton",
        "LispMarketModuleButton",
        "ShortcutModuleButton"
    };
    var moduleButtonOrder = xamlDocument.Descendants()
        .Where(element => element.Name.LocalName == "Button")
        .Select(element => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name")?.Value)
        .Where(name => name is not null && expectedModuleOrder.Contains(name, StringComparer.Ordinal))
        .ToArray();
    Assert(moduleButtonOrder.SequenceEqual(expectedModuleOrder, StringComparer.Ordinal),
        "左侧主功能导航顺序与约定不一致。");
    var textColumns = xamlDocument.Descendants()
        .Where(element => element.Name.LocalName == "DataGridTextColumn")
        .ToList();
    var shortcutDescriptionColumn = textColumns.First(element => (string?)element.Attribute("Header") == "功能说明");
    var defaultColumn = textColumns.Single(element => (string?)element.Attribute("Header") == "默认");
    var customColumn = textColumns.Single(element => (string?)element.Attribute("Header") == "自定义");
    var coreCommandColumn = textColumns.Single(element => (string?)element.Attribute("Header") == "底层命令");
    Assert(xaml.Contains("Text=\"快捷键\"", StringComparison.Ordinal), "左侧缺少快捷键一级模块。") ;
    Assert(xaml.Contains("Text=\"插入图片\"", StringComparison.Ordinal), "左侧缺少插入图片一级模块。") ;
    Assert(xaml.Contains("Content=\"批量插入图片 (DCOLE)\"", StringComparison.Ordinal) &&
           xaml.Contains("Click=\"OnInsertImagesClick\"", StringComparison.Ordinal), "批量插入图片入口尚未启用。");
    Assert(xaml.Contains("Header=\"重命名快捷键\"", StringComparison.Ordinal), "缺少重命名快捷键标签。") ;
    Assert(xaml.Contains("Header=\"CAD命令大全\"", StringComparison.Ordinal), "缺少CAD命令大全标签。") ;
    Assert(xaml.Contains("Content=\"＋ 添加映射\"", StringComparison.Ordinal) &&
           xaml.Contains("Click=\"OnAddMappingClick\"", StringComparison.Ordinal), "添加映射入口尚未启用。") ;
    Assert(xaml.Contains("Content=\"删除选中\"", StringComparison.Ordinal) &&
           xaml.Contains("Click=\"OnDeleteSelectedClick\"", StringComparison.Ordinal), "删除选中入口尚未启用。") ;
    Assert(xaml.Contains("Content=\"导出 CSV\"", StringComparison.Ordinal) &&
           xaml.Contains("Click=\"OnExportCsvClick\"", StringComparison.Ordinal), "导出CSV入口尚未启用。") ;
    Assert(!xaml.Contains("Header=\"分类\"", StringComparison.Ordinal), "快捷键表仍显示分类列。") ;
    Assert(!xaml.Contains("<GridSplitter", StringComparison.Ordinal),
        "左侧功能区仍使用会在 AutoCAD PaletteSet 中触发原生崩溃的 WPF GridSplitter。") ;
    var moduleSplitter = xamlDocument.Descendants()
        .Single(element => element.Name.LocalName == "Thumb" &&
                           element.Attributes().Any(attribute =>
                               attribute.Name.LocalName == "Name" &&
                               (string?)attribute == "ModuleNavigationSplitter"));
    Assert((string?)moduleSplitter.Attribute("DragDelta") == "OnModuleNavigationSplitterDragDelta" &&
           xaml.Contains("x:Name=\"ModuleNavigationColumn\"", StringComparison.Ordinal) &&
           xaml.Contains("x:Name=\"ModuleContentColumn\"", StringComparison.Ordinal),
        "左侧功能区没有使用受边界约束的自定义拖动条。") ;
    var rootMinimumWidth = double.Parse(
        (string?)xamlDocument.Root?.Attribute("MinWidth") ?? "0",
        CultureInfo.InvariantCulture);
    var navigationColumn = xamlDocument.Descendants()
        .Single(element => element.Name.LocalName == "ColumnDefinition" &&
                           element.Attributes().Any(attribute =>
                               attribute.Name.LocalName == "Name" &&
                               attribute.Value == "ModuleNavigationColumn"));
    var contentColumn = xamlDocument.Descendants()
        .Single(element => element.Name.LocalName == "ColumnDefinition" &&
                           element.Attributes().Any(attribute =>
                               attribute.Name.LocalName == "Name" &&
                               attribute.Value == "ModuleContentColumn"));
    Assert(rootMinimumWidth <= 0 &&
           double.Parse((string?)navigationColumn.Attribute("MinWidth") ?? "0", CultureInfo.InvariantCulture) <= 0 &&
           double.Parse((string?)contentColumn.Attribute("MinWidth") ?? "0", CultureInfo.InvariantCulture) <= 0,
        "工具箱外框或双栏布局仍把最小宽度锁在宽面板范围，无法缩成窄栏模式。") ;
    var narrowSearchBoxes = xamlDocument.Descendants()
        .Where(element => element.Name.LocalName == "TextBox" &&
                          element.Attributes().Any(attribute =>
                              attribute.Name.LocalName == "Name" &&
                              (attribute.Value == "SearchBox" || attribute.Value == "CadSearchBox")));
    Assert(narrowSearchBoxes.All(element =>
            double.Parse((string?)element.Attribute("MinWidth") ?? "0", CultureInfo.InvariantCulture) <= 140),
        "快捷键搜索框仍使用固定大宽度，会继续把窄面板顶宽。");
    var toolboxPalette = File.ReadAllText(Path.Combine(Path.GetDirectoryName(xamlPath)!, "ToolboxPalette.cs"));
    Assert(toolboxPalette.Contains("MinimumSize = new DrawingSize(32, 360)", StringComparison.Ordinal),
        "AutoCAD PaletteSet 仍把工具箱最小宽度锁在 430px。") ;
    Assert(xaml.Contains("TextWrapping=\"Wrap\"", StringComparison.Ordinal), "左侧长功能名称未启用完整换行。") ;
    Assert(theme.Contains("x:Key=\"ThemeTextPrimaryBrush\"", StringComparison.Ordinal) &&
           theme.Contains("Color=\"#20242A\"", StringComparison.Ordinal) &&
           xaml.Contains("BasedOn=\"{StaticResource DcButtonStyle}\"", StringComparison.Ordinal),
        "左侧选中项未使用全局深色文字主题。") ;
    Assert(CountOccurrences(xaml, "CanUserResizeColumns=\"True\"") == 2, "表格未全部启用列宽拖动。") ;
    var shortcutDataGrids = xamlDocument.Descendants()
        .Where(element => element.Name.LocalName == "DataGrid")
        .Where(element => element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" &&
            ((string?)attribute == "ShortcutGrid" || (string?)attribute == "CadCommandGrid")))
        .ToList();
    Assert(shortcutDataGrids.Count == 2 && shortcutDataGrids.All(element =>
        (string?)element.Attribute("ScrollViewer.HorizontalScrollBarVisibility") == "Auto"),
        "快捷键与CAD命令表格未全部启用横向溢出滚动。");
    Assert(theme.Contains("<Setter Property=\"MinColumnWidth\" Value=\"48\" />", StringComparison.Ordinal) &&
           theme.Contains("<Setter Property=\"CanUserResizeColumns\" Value=\"True\" />", StringComparison.Ordinal) &&
           theme.Contains("<Setter Property=\"ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Auto\" />", StringComparison.Ordinal),
        "全局表格样式没有统一最小列宽、列宽拖动和窄面板横向滚动契约。");
    foreach (var gridName in new[] { "ShortcutGrid", "CadCommandGrid", "RadialMenuGrid", "LispPluginGrid" })
    {
        var columns = GetDataGridColumns(FindNamedDataGrid(xamlDocument, gridName));
        Assert(columns.Length > 0 && columns.All(HasReadableMinimumWidth),
            $"{gridName} 仍有列可被拖到遮挡标题或内容。");
        Assert(columns.All(column => ((string?)column.Attribute("Width"))?.Contains('*') == true),
            $"{gridName} 没有让全部内容列跟随外框按比例伸缩。");
    }

    var viewsRoot = Path.GetDirectoryName(xamlPath)!;
    foreach (var fileName in new[] { "BatchPrintWindow.xaml", "PdfToolWindow.xaml" })
    {
        var toolWindowDocument = XDocument.Parse(File.ReadAllText(Path.Combine(viewsRoot, fileName)));
        var unsafeFixedColumns = toolWindowDocument.Descendants()
            .Where(element => element.Parent?.Name.LocalName == "DataGrid.Columns" &&
                              element.Name.LocalName.EndsWith("Column", StringComparison.Ordinal))
            .Where(column => !(((string?)column.Attribute("Width"))?.Contains('*') == true))
            .Where(column => !HasReadableMinimumWidth(column))
            .Select(column => (string?)column.Attribute("Header") ?? "(无标题)")
            .ToArray();
        Assert(unsafeFixedColumns.Length == 0,
            $"{fileName} 仍有固定列可被缩到遮挡标题：{string.Join(", ", unsafeFixedColumns)}。");
    }

    var layerManager = File.ReadAllText(Path.Combine(viewsRoot, "LayerManagerWindow.cs"));
    Assert(CountOccurrences(layerManager, "MinWidth =") >= 3,
        "图层管理器的三列表格没有全部设置最小可读宽度。");
    Assert(xaml.Contains("CellStyle=\"{StaticResource MutedCellStyle}\"", StringComparison.Ordinal), "底层命令列未使用灰色文字。") ;
    Assert((string?)shortcutDescriptionColumn.Attribute("IsReadOnly") != "True", "功能说明仍不可编辑。") ;
    Assert((string?)defaultColumn.Attribute("IsReadOnly") != "True", "默认快捷键仍不可编辑。") ;
    Assert((string?)customColumn.Attribute("IsReadOnly") != "True", "自定义快捷键仍不可编辑。") ;
    Assert((string?)coreCommandColumn.Attribute("IsReadOnly") == "True", "底层命令未保持只读。") ;
    Assert(xaml.Contains("x:Key=\"GridTextBlockStyle\"", StringComparison.Ordinal) &&
           xaml.Contains("BasedOn=\"{StaticResource DcTextBlockStyle}\"", StringComparison.Ordinal) &&
           theme.Contains("<Setter Property=\"VerticalAlignment\" Value=\"Center\" />", StringComparison.Ordinal),
        "表格文字未显式设置垂直居中。") ;
    Assert(xaml.Contains("<Style x:Key=\"GridCellStyle\"", StringComparison.Ordinal) &&
           xaml.Contains("BasedOn=\"{StaticResource DcDataGridCellStyle}\"", StringComparison.Ordinal) &&
           theme.Contains("<Setter Property=\"Foreground\" Value=\"{StaticResource ThemeTextPrimaryBrush}\" />", StringComparison.Ordinal),
        "普通单元格未固定深色文字，活动选中时可能变白。") ;
    Assert(xaml.Contains("SetColumnMinimumWidth", StringComparison.Ordinal) ||
           File.ReadAllText(Path.ChangeExtension(xamlPath, ".xaml.cs")).Contains("SetColumnMinimumWidth", StringComparison.Ordinal),
        "表格缺少按内容保护最小列宽的逻辑。") ;
    Assert(!xaml.Contains("TextTrimming=", StringComparison.Ordinal), "面板不应使用文字省略显示。") ;
    Assert(File.ReadAllText(shortcutManagerPath).Contains(
        "item.DefaultAlias = ShortcutValidator.NormalizeAlias(item.DefaultAlias);",
        StringComparison.Ordinal), "可编辑的默认快捷键未在保存时标准化。") ;
    var imageCommands = File.ReadAllText(imageCommandsPath);
    Assert(imageCommands.Contains("[CommandMethod(\"DC_CMD_IMAGE_BATCH\"", StringComparison.Ordinal),
        "批量图片功能未使用DC_CMD_底层命令前缀。") ;
    Assert(imageCommands.Contains("[ShortcutCommand(\"插入图片\", \"批量插入图片\", \"DCOLE\")]", StringComparison.Ordinal),
        "批量图片功能未登记默认OLE快捷键。") ;
}

static void InstallerAutoCadShutdownFlowIsSafe()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var installerPath = Path.Combine(root, "packaging", "Installer", "Program.cs");
    var installer = File.ReadAllText(installerPath);

    Assert(installer.Contains("EnsureAutoCadClosed", StringComparison.Ordinal),
        "安装器仍在发现AutoCAD后直接失败，没有进入可恢复的关闭流程。");
    Assert(installer.Contains("重新检测", StringComparison.Ordinal) &&
           installer.Contains("强制关闭并继续", StringComparison.Ordinal) &&
           installer.Contains("取消安装", StringComparison.Ordinal),
        "AutoCAD占用提示缺少重新检测、强制关闭并继续或取消安装入口。");
    Assert(installer.Contains("未保存的图纸修改会丢失", StringComparison.Ordinal),
        "强制关闭AutoCAD前没有明确的未保存内容二次警告。");
    Assert(installer.Contains("TryForceCloseAutoCadProcesses", StringComparison.Ordinal) &&
           installer.Contains("IsVerifiedAutoCadExecutable", StringComparison.Ordinal) &&
           installer.Contains("Path.GetFileName", StringComparison.Ordinal),
        "强制关闭没有校验acad.exe的完整可执行文件身份。");
}

static void InstallerWizardSupportsScopePathAndProgress()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var installerRoot = Path.Combine(root, "packaging", "Installer");
    var program = File.ReadAllText(Path.Combine(installerRoot, "Program.cs"));
    var wizardPath = Path.Combine(installerRoot, "InstallerWizardForm.cs");

    Assert(File.Exists(wizardPath), "安装器仍是消息框流程，缺少安装向导界面。");
    var wizard = File.ReadAllText(wizardPath);
    Assert(wizard.Contains("当前用户（推荐）", StringComparison.Ordinal) &&
           wizard.Contains("所有用户（需要管理员权限）", StringComparison.Ordinal) &&
           wizard.Contains("安装位置", StringComparison.Ordinal),
        "安装向导没有提供两种AutoCAD标准安装范围和实际路径显示。");
    Assert(wizard.Contains("正在准备安装", StringComparison.Ordinal) &&
           wizard.Contains("安装完成", StringComparison.Ordinal) &&
           wizard.Contains("旧版本备份", StringComparison.Ordinal),
        "安装向导缺少安装进度或完成路径页面。");
    Assert(program.Contains("SpecialFolder.CommonApplicationData", StringComparison.Ordinal) &&
           program.Contains("SpecialFolder.ApplicationData", StringComparison.Ordinal) &&
           program.Contains("Verb = \"runas\"", StringComparison.Ordinal) &&
           program.Contains("--all-users", StringComparison.Ordinal),
        "安装器没有按范围计算标准ApplicationPlugins路径，或所有用户安装没有申请管理员权限。");
}

static void InstallerUpgradePreservesUserResources()
{
    var root = Path.Combine(Path.GetTempPath(), "YiZhiMaoDc-installer-merge-" + Guid.NewGuid().ToString("N"));
    try
    {
        var oldBundle = Path.Combine(root, "old", "YiZhiMaoDc.bundle");
        var newBundle = Path.Combine(root, "new", "YiZhiMaoDc.bundle");
        var oldModules = Path.Combine(oldBundle, "Contents", "Modules");
        var newModules = Path.Combine(newBundle, "Contents", "Modules");
        Directory.CreateDirectory(oldModules);
        Directory.CreateDirectory(newModules);

        var oldBuiltIn = Path.Combine(oldModules, "built-in.dwg");
        var removedBuiltIn = Path.Combine(oldModules, "removed-built-in.dwg");
        var modifiedRemoved = Path.Combine(oldModules, "modified-removed.dwg");
        File.WriteAllText(oldBuiltIn, "old-original");
        File.WriteAllText(removedBuiltIn, "old-packaged");
        File.WriteAllText(modifiedRemoved, "original-before-user-change");
        File.WriteAllLines(
            Path.Combine(oldBundle, "bundle-hashes.sha256"),
            new[]
            {
                Sha256Line(oldBundle, oldBuiltIn),
                Sha256Line(oldBundle, removedBuiltIn),
                Sha256Line(oldBundle, modifiedRemoved)
            });

        File.WriteAllText(oldBuiltIn, "user-modified-old");
        File.WriteAllText(modifiedRemoved, "user-modified-removed");
        var customFile = Path.Combine(oldModules, "FrameTemplates", "我的图框.dwg");
        Directory.CreateDirectory(Path.GetDirectoryName(customFile)!);
        File.WriteAllText(customFile, "custom-frame");
        File.WriteAllText(Path.Combine(newModules, "built-in.dwg"), "new-version");

        var result = YiZhiMao.Dc.Installer.InstallerFileOperations.MergeUserResources(
            oldBundle,
            newBundle);

        Assert(File.ReadAllText(Path.Combine(newModules, "built-in.dwg")) == "new-version",
            "升级时旧版同名资源覆盖了新版内置资源。") ;
        Assert(!File.Exists(Path.Combine(newModules, "removed-built-in.dwg")),
            "已从新版移除且用户未修改的内置资源被错误恢复。") ;
        Assert(File.ReadAllText(Path.Combine(newModules, "modified-removed.dwg")) == "user-modified-removed" &&
               File.ReadAllText(Path.Combine(newModules, "FrameTemplates", "我的图框.dwg")) == "custom-frame",
            "用户新增或修改且新版不存在的资源没有恢复到活动Modules。") ;
        Assert(result.AddedCount == 1 && result.ModifiedRestoredCount == 1 && result.ConflictCount == 1,
            "资源恢复数量或同名冲突数量不正确。") ;
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void InstallerUninstallPreservesDataByDefault()
{
    var root = Path.Combine(Path.GetTempPath(), "YiZhiMaoDc-installer-uninstall-" + Guid.NewGuid().ToString("N"));
    try
    {
        var bundle = Path.Combine(root, "YiZhiMaoDc.bundle");
        var modules = Path.Combine(bundle, "Contents", "Modules");
        var preserved = Path.Combine(root, "preserved");
        Directory.CreateDirectory(modules);
        var packaged = Path.Combine(modules, "packaged.dwg");
        var modified = Path.Combine(modules, "modified.lsp");
        File.WriteAllText(packaged, "packaged");
        File.WriteAllText(modified, "original");
        File.WriteAllLines(
            Path.Combine(bundle, "bundle-hashes.sha256"),
            new[] { Sha256Line(bundle, packaged), Sha256Line(bundle, modified) });
        File.WriteAllText(modified, "user-change");
        var added = Path.Combine(modules, "LispPlugins", "custom.lsp");
        Directory.CreateDirectory(Path.GetDirectoryName(added)!);
        File.WriteAllText(added, "custom");

        var result = YiZhiMao.Dc.Installer.InstallerFileOperations.ExportUserResources(
            bundle,
            preserved);

        Assert(!File.Exists(Path.Combine(preserved, "Contents", "Modules", "packaged.dwg")),
            "卸载备份不应复制未修改的内置资源。") ;
        Assert(File.Exists(Path.Combine(preserved, "Contents", "Modules", "modified.lsp")) &&
               File.Exists(Path.Combine(preserved, "Contents", "Modules", "LispPlugins", "custom.lsp")) &&
               result.PreservedCount == 2,
            "卸载没有保留用户修改和新增的资源。") ;

        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var wizard = File.ReadAllText(Path.Combine(projectRoot, "packaging", "Installer", "InstallerWizardForm.cs"));
        Assert(wizard.Contains("同时删除当前用户配置和自定义资源", StringComparison.Ordinal) &&
               wizard.Contains("Checked = false", StringComparison.Ordinal),
            "卸载界面缺少默认不勾选的用户数据清理选项。") ;
    }
    finally
    {
        DeleteTestDirectory(root);
    }
}

static void InstallerFailureRollsBackAndWritesLog()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var program = File.ReadAllText(Path.Combine(root, "packaging", "Installer", "Program.cs"));
    var wizard = File.ReadAllText(Path.Combine(root, "packaging", "Installer", "InstallerWizardForm.cs"));

    Assert(program.Contains("RollbackInstallation", StringComparison.Ordinal) &&
           program.Contains("Directory.Delete(targetBundle, true)", StringComparison.Ordinal) &&
           program.Contains("Directory.Move(backupBundle, targetBundle)", StringComparison.Ordinal),
        "安装失败没有删除半安装的新Bundle并恢复旧Bundle。") ;
    Assert(program.Contains("InstallerLog", StringComparison.Ordinal) &&
           program.Contains("LogPath", StringComparison.Ordinal),
        "安装和卸载流程没有记录可定位问题的日志。") ;
    Assert(program.Contains("bundleBackup", StringComparison.Ordinal) &&
           program.Contains("userDataBackup", StringComparison.Ordinal) &&
           program.Contains("RestoreRegistryKey", StringComparison.Ordinal) &&
           program.Contains("Directory.Move(bundleBackup, targetBundle)", StringComparison.Ordinal),
        "卸载失败没有恢复Bundle、用户数据和卸载注册信息。") ;
    Assert(program.Contains("CaptureRegistryKey", StringComparison.Ordinal) &&
           program.Contains(".rollback-", StringComparison.Ordinal),
        "卸载入口注册失败没有事务式恢复原注册信息和安装器副本。") ;
    Assert(wizard.Contains("保留的用户资源", StringComparison.Ordinal) &&
           wizard.Contains("安装日志", StringComparison.Ordinal),
        "安装完成页没有显示用户资源保留数量或安装日志路径。") ;
}

static string Sha256Line(string bundleRoot, string filePath)
{
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    using var stream = File.OpenRead(filePath);
    var hash = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    var relative = Path.GetRelativePath(bundleRoot, filePath).Replace('\\', '/');
    return hash + " *" + relative;
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void DeleteTestDirectory(string root)
{
    var resolvedRoot = Path.GetFullPath(root);
    var resolvedTemp = Path.GetFullPath(Path.GetTempPath());
    if (resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedRoot))
    {
        Directory.Delete(resolvedRoot, recursive: true);
    }
}

static int CountOccurrences(string value, string pattern)
{
    var count = 0;
    var offset = 0;
    while ((offset = value.IndexOf(pattern, offset, StringComparison.Ordinal)) >= 0)
    {
        count++;
        offset += pattern.Length;
    }

    return count;
}

static ShortcutItem CreateItem(
    string description,
    string coreCommand,
    string defaultAlias = "",
    string customAlias = "")
{
    return new ShortcutItem
    {
        Category = "测试",
        Description = description,
        CoreCommand = coreCommand,
        DefaultAlias = defaultAlias,
        CustomAlias = customAlias
    };
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
