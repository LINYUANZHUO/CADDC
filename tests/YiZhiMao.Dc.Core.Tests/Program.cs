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
    ("非法用户配置仍可加载到面板修正", InvalidSavedAliasCanStillBeLoaded),
    ("空命令列表配置可恢复默认值", NullSavedItemsRecoverDefaults),
    ("新增映射使用NEWCMD默认值", NewMappingUsesRequestedDefaults),
    ("用户附加映射可随配置合并保留", UserDefinedMappingSurvivesConfigurationMerge),
    ("用户附加映射必须填写快捷键", UserDefinedMappingRequiresAlias),
    ("CSV导出使用UTF8并正确转义", CsvExportUsesUtf8AndEscapesFields),
    ("CAD命令大全包含预期分类和命令", CadCommandCatalogHasExpectedContent),
    ("CAD命令大全支持中英文搜索", CadCommandCatalogSearchesChineseAndEnglish),
    ("CAD命令大全空搜索显示全部", EmptyCadCommandSearchMatchesAll),
    ("批量图片参数支持默认值和等比缩放", ImageBatchOptionsScaleProportionally),
    ("批量图片参数拒绝非法间距和高度", ImageBatchOptionsRejectInvalidValues),
    ("批量图片按实际宽度和间距向右排列", ImageBatchOptionsCalculateNextPosition),
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
    ("抽面默认竖向两块固定门缝且遮罩关闭", DrawerFaceOptionsUseConfirmedDefaults),
    ("抽面按方向均分并应用四向盖值", DrawerFaceLayoutSupportsVerticalAndHorizontalDirections),
    ("抽面设置、拾取应用拉手和DCMC命令入口完整", DrawerFaceSurfaceAndCommandAreComplete),
    ("抽面首次预览刷新且Release构建包含Core", DrawerFacePreviewAndReleaseConfigurationAreComplete),
    ("抽面开门箭头向下且左右键始终控制数量", DrawerFaceOpeningArrowAndHotkeysFollowDrawingWorkflow),
    ("门板线色遮罩与图层图纸模块入口完整", DoorColorMaskAndDrawingLayerSurfaceAreComplete),
    ("图纸修复遍历本地图块并重算墙板填充", DrawingDisplayRepairTraversesAllLocalBlocksAndReevaluatesHatches),
    ("板件模块包含板件单双线和组件三个页签", BoardModuleSurfaceAndCommandsAreComplete),
    ("板件高级操作包含位移夹层均分背板和编辑投影", AdvancedBoardOperationsAreComplete),
    ("拆单统计菜单包含五类统计入口", StatisticsModuleSurfaceAndCommandsAreComplete),
    ("拆单统计按规格汇总并输出Excel兼容CSV", DrawingStatisticsAggregateAndExport),
    ("一键全屋拆单支持CAD表格与Excel总表", WholeHouseStatisticsSupportsCadTableAndExcel),
    ("圆盘默认三圈配置与命中规则稳定", RadialMenuDefaultsAndHitTestingAreStable),
    ("圆盘配置持久化并拒绝非法参数", RadialMenuOptionsPersistAndRejectInvalidValues),
    ("圆盘设置面板与底层命令入口完整", RadialMenuSurfaceAndCommandAreComplete),
    ("圆盘由鼠标中键双击触发并替代默认缩放范围", RadialMenuMiddleButtonActivationIsRegistered),
    ("LISP市场默认本地库与三种加载指南完整", LispMarketSurfaceContractIsComplete),
    ("本地LISP库扫描受支持文件并生成安全加载表达式", LispPluginLibraryScansAndBuildsLoadExpression),
    ("本地LISP命令在插件库中保持唯一", LispPluginCommandsAreUnique),
    ("一键图框参数校验并按内框计算缩放落位", FrameToolOptionsAndPlacementAreValid),
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
    ("Modules资源会打包并由插件直接读取", ModuleResourcesAreBundledAndReadDirectly),
    ("Bundle构建前清理全部旧Contents", BundleBuildCleansAllPreviousContents),
    ("工具箱XAML静态资源全部可解析", ShortcutPanelStaticResourcesAreDefined),
    ("快捷键面板保留双层导航和防截断约束", ShortcutPanelLayoutKeepsNavigationAndOverflowContract),
    ("打印设置可持久化并拒绝非法分辨率", PrintSettingsPersistAndValidate),
    ("打印选项只返回设备实际支持的值", PrintOptionsChooseOnlyAvailableValues),
    ("打印图框去重去嵌套并按阅读顺序排列", PrintFramesAreCleanedAndSorted),
    ("PDF工具扫描页数并按选择顺序合并", PdfToolScansAndMergesInOrder),
    ("批量打印命令面板与PDF工具入口完整", BatchPrintSurfaceContractIsComplete),
    ("批量打印避免布局字典强转并保留阶段错误", BatchPrintRuntimeRegressionContract),
    ("批量打印窄面板按钮与窗口坐标保持有效", BatchPrintNarrowPanelAndWindowAreaContract),
    ("批量打印按布局读取纸张并先写窗口后切换类型", BatchPrintUsesLayoutBoundMediaAndWindowSequence),
    ("纸张打印排除文件设备且批量任务关闭后台打印", BatchPrintSeparatesPaperDevicesAndDisablesBackgroundPlot),
    ("D3和D4只打印当前布局且D1提示打印服务前置条件", BatchPrintTargetsCurrentLayoutAndChecksPaperPrerequisites),
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
    Assert(item.DefaultAlias == "NEWCMD", "新增映射的默认快捷键不正确。");
    Assert(item.CustomAlias == "NEWCMD", "新增映射的自定义快捷键不正确。");
    Assert(item.CoreCommand == "NEWCMD", "新增映射的底层命令不正确。");
    Assert(item.IsUserDefined, "新增映射未标记为用户映射。");
    Assert(ShortcutValidator.GetEffectiveAliases(item).SequenceEqual(["NEWCMD"]), "相同的默认和自定义快捷键未合并为一个映射。");

    var script = AutoLispShortcutGenerator.Generate(new ShortcutConfiguration { ShortcutItems = [item] });
    Assert(script.Contains(".NEWCMD", StringComparison.Ordinal), "新增映射生成的LSP未调用NEWCMD底层命令。");
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
        Assert(userMapping.CoreCommand == "NEWCMD", "用户映射的底层命令未保留。");
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
        Assert(text.Contains("新功能,NEWCMD,NEWCMD,否,NEWCMD\r\n", StringComparison.Ordinal), "CSV缺少新增映射默认值。");
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
                 "DoorPanelLeftExtensionBox", "DoorPanelRightExtensionBox"
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
           panelCode.Contains("DC_CMD_ELEVATION_DOOR", StringComparison.Ordinal),
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
    Assert(defaults.Orientation == DrawerFaceOrientation.Vertical && defaults.Count == 2 &&
           defaults.Gap == 3 && !defaults.GenerateMask && !defaults.GenerateHandle,
        "抽面必须默认竖向两块、3mm固定缝，并默认关闭背景遮罩和拉手。" );
    Assert(DrawerFaceOptions.TryCreate(
            DrawerFaceOrientation.Horizontal,
            "3", "3.2", "10", "20", "30", "40", true, true,
            out var parsed,
            out var error) &&
           parsed.Orientation == DrawerFaceOrientation.Horizontal && parsed.Count == 3 && parsed.Gap == 3.2 &&
           parsed.TopExtension == 10 && parsed.BottomExtension == 20 &&
           parsed.LeftExtension == 30 && parsed.RightExtension == 40 &&
           parsed.GenerateMask && parsed.GenerateHandle,
        $"有效抽面参数未通过验证：{error}" );
    Assert(!DrawerFaceOptions.TryCreate(
            DrawerFaceOrientation.Vertical,
            "0", "3", "0", "0", "0", "0", false, false,
            out _, out var countError) && countError.Contains("数量", StringComparison.Ordinal),
        "抽面没有拒绝小于1的数量。" );
}

static void DrawerFaceLayoutSupportsVerticalAndHorizontalDirections()
{
    var vertical = DrawerFaceLayoutCalculator.Calculate(
        new DrawerFacePoint(100, 200),
        new DrawerFacePoint(900, 1200),
        DrawerFaceOptions.Default with
        {
            TopExtension = 20,
            BottomExtension = 10,
            LeftExtension = 5,
            RightExtension = 15
        });
    Assert(vertical.Faces.Count == 2 && Math.Abs(vertical.ActualGap - 10d / 3) < 0.000001 &&
           vertical.FaceWidth == 820 && Math.Abs(vertical.FaceHeight - 513.3333333333334) < 0.000001,
        "竖向抽面没有按统一门扇数补偿门缝从下到上均分。" );
    Assert(Math.Abs(vertical.Faces[0].X - 95) < 0.000001 &&
           Math.Abs(vertical.Faces[0].Y - 190) < 0.000001 &&
           Math.Abs(vertical.Faces[1].Y - 706.6666666666667) < 0.000001,
        "竖向抽面的四向盖值、顺序或位置不正确。" );
    Assert(Math.Abs(vertical.HandleCenters[0].Y - 446.6666666666667) < 0.000001 &&
           Math.Abs(vertical.HandleCenters[1].Y - 963.3333333333334) < 0.000001,
        "竖向抽面的拉手中心没有落在各抽面中心。" );

    var horizontal = DrawerFaceLayoutCalculator.Calculate(
        new DrawerFacePoint(0, 0),
        new DrawerFacePoint(1000, 600),
        DrawerFaceOptions.Default with
        {
            Orientation = DrawerFaceOrientation.Horizontal,
            Count = 3,
            Gap = 4
        });
    Assert(horizontal.Faces.Count == 3 && horizontal.ActualGap == 4.5 &&
           horizontal.FaceWidth == 330.3333333333333 && horizontal.FaceHeight == 600,
        "横向抽面没有按统一门扇数补偿门缝从左到右均分。" );
    Assert(Math.Abs(horizontal.Faces[2].Right - 1000) < 0.000001,
        "横向抽面的最后一块没有精确收口到右边界。" );
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
           panel.Contains("x:Name=\"DrawerFaceVerticalRadio\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceGapBox\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceGenerateMaskCheck\"", StringComparison.Ordinal) &&
           panel.Contains("拾取拉手 (DCSDH)", StringComparison.Ordinal) &&
           panel.Contains("应用/替换拉手 (DCLAS)", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceCalculationModeCombo\"", StringComparison.Ordinal) &&
           panel.Contains("x:Name=\"DrawerFaceActualSizeText\"", StringComparison.Ordinal) &&
           panel.Contains("生成抽面 (DCMC)", StringComparison.Ordinal),
        "门板抽面模块缺少抽面方向、计算模式、遮罩、拾取应用拉手、实际尺寸或生成入口。" );
    Assert(panelCode.Contains("DrawerFaceOptions.TryCreate", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_HANDLE_PICK", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_HANDLE_APPLY", StringComparison.Ordinal) &&
           panelCode.Contains("DC_CMD_DRAWER_FACE", StringComparison.Ordinal),
        "抽面面板没有读取独立参数、多拉手素材库或调用底层命令。" );
    Assert(commands.Contains("[CommandMethod(\"DC_CMD_DRAWER_FACE\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"门板抽面\", \"生成抽面\", \"DCMC\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DCMC\"", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyUp", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyDown", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyLeft", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyRight", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyAdd", StringComparison.Ordinal) &&
           commands.Contains("Wipeout", StringComparison.Ordinal),
        "DCMC缺少原生命令、方向键/加减热键或遮罩落图。" );
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
    Assert(commands.Contains("VirtualKeyRight => 1", StringComparison.Ordinal) &&
           commands.Contains("VirtualKeyLeft => -1", StringComparison.Ordinal),
        "左右方向键没有在两种排列方向下固定控制抽面数量。" );
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

static void BoardModuleSurfaceAndCommandsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "BoardCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    Assert(panel.Contains("x:Name=\"BoardModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"板件\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"单双线\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"组件\"", StringComparison.Ordinal),
        "板件主模块或三个已确认页签不完整。" );
    Assert(commands.Contains("DC_CMD_BOARD_DRAW", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_BOARD_CENTER", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_SINGLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_DOUBLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_TRIPLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_TO_DOUBLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_LINE_TO_TRIPLE", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_WINE_RACK_CROSS", StringComparison.Ordinal) &&
           commands.Contains("DC_CMD_WINE_RACK_DIAGONAL", StringComparison.Ordinal),
        "板件、单双线或组件底层命令入口不完整。" );
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.BoardCommands))", StringComparison.Ordinal),
        "板件命令类没有注册到插件。" );
}

static void AdvancedBoardOperationsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "AdvancedBoardCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    string[] commandNames =
    [
        "DC_CMD_BOARD_DISPLACE", "DC_CMD_BOARD_ABA", "DC_CMD_BOARD_DIVIDE", "DC_CMD_BOARD_BACKBOARD",
        "DC_CMD_BOARD_SPLIT", "DC_CMD_BOARD_MERGE", "DC_CMD_BOARD_EXTEND", "DC_CMD_BOARD_TRIM",
        "DC_CMD_BOARD_SHELF_PROJECT"
    ];
    Assert(commandNames.All(name => commands.Contains(name, StringComparison.Ordinal)),
        "板件高级操作底层命令不完整。");
    Assert(panel.Contains("位移板", StringComparison.Ordinal) &&
           panel.Contains("ABA夹层", StringComparison.Ordinal) &&
           panel.Contains("均分板件", StringComparison.Ordinal) &&
           panel.Contains("背板生成", StringComparison.Ordinal) &&
           panel.Contains("拆分板件", StringComparison.Ordinal) &&
           panel.Contains("合并板件", StringComparison.Ordinal) &&
           panel.Contains("延伸线/矩形", StringComparison.Ordinal) &&
           panel.Contains("修剪线/矩形", StringComparison.Ordinal) &&
           panel.Contains("层板投影", StringComparison.Ordinal),
        "板件高级操作界面入口不完整。");
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.AdvancedBoardCommands))", StringComparison.Ordinal),
        "板件高级命令类没有注册到插件。");
}

static void StatisticsModuleSurfaceAndCommandsAreComplete()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var panel = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml"));
    var commands = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "StatisticsCommands.cs"));
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

    Assert(panel.Contains("x:Name=\"StatisticsModuleView\"", StringComparison.Ordinal) &&
           panel.Contains("Header=\"部件统计\"", StringComparison.Ordinal) &&
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
    Assert(commands.Contains("polyline.Layer.Contains(\"柜体结构\"", StringComparison.Ordinal) &&
           commands.Contains("var boardThickness = Math.Min(width, height)", StringComparison.Ordinal) &&
           !commands.Contains("combined.Contains(\"板\"", StringComparison.Ordinal),
        "柜体板件没有按长度×深度×板厚换算，或普通门板仍会混入柜体拆单。");
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.StatisticsCommands))", StringComparison.Ordinal),
        "拆单统计命令类没有注册到插件。");
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
    Assert(commands.Contains("DC_CMD_WHOLE_HOUSE_BOM", StringComparison.Ordinal) &&
           commands.Contains("放置CAD表格", StringComparison.Ordinal) &&
           commands.Contains("导出Excel总表", StringComparison.Ordinal) &&
           commands.Contains("new Table", StringComparison.Ordinal) &&
           commands.Contains("Drawing1_柜拆单.xls", StringComparison.Ordinal),
        "一键全屋拆单没有同时提供CAD表格和.xls总表输出。");
}

static void RadialMenuDefaultsAndHitTestingAreStable()
{
    var options = RadialMenuOptions.CreateDefault();
    options.Validate();
    Assert(options.Enabled && options.AutoCloseAfterCommand && options.ShowThirdRing &&
           options.Radius == 200 && Math.Abs(options.Opacity - 1) < 0.000001,
        "圆盘默认开关、大小或透明度与确认界面不一致。");
    Assert(options.Items.Count(item => item.Ring == 1) == 8 &&
           options.Items.Count(item => item.Ring == 2) == 16 &&
           options.Items.Count(item => item.Ring == 3) == 24,
        "圆盘默认配置不是8/16/24三个完整圆环。");

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
        options.Items.Single(item => item.Ring == 1 && item.Position == 1).CommandAlias = "DCMB";
        RadialMenuOptionsStore.Save(path, options);
        var loaded = RadialMenuOptionsStore.Load(path);
        Assert(loaded.Radius == 220 && Math.Abs(loaded.Opacity - 0.8) < 0.000001 &&
               loaded.Items.Single(item => item.Ring == 1 && item.Position == 1).CommandAlias == "DCMB",
            "圆盘大小、透明度或命令配置没有持久化。");

        loaded.Radius = 50;
        AssertThrows<InvalidOperationException>(loaded.Validate, "非法圆盘大小未被拒绝。");
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
    var application = File.ReadAllText(Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "PluginApplication.cs"));

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
    Assert(application.Contains("CommandClass(typeof(YiZhiMao.Dc.Plugin.RadialMenuCommands))", StringComparison.Ordinal),
        "圆盘命令类没有注册到插件。");
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
           commands.Contains("[ShortcutCommand(\"五金件\", \"插入灯带\", \"DD\")]", StringComparison.Ordinal) &&
           commands.Contains("[CommandMethod(\"DC_CMD_HINGE_INSERT\"", StringComparison.Ordinal) &&
           commands.Contains("[ShortcutCommand(\"门板抽面\", \"插入铰链\", \"JL\")]", StringComparison.Ordinal) &&
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

static void ModuleResourcesAreBundledAndReadDirectly()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var modulesPath = Path.Combine(root, "Modules");
    var projectPath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "YiZhiMao.Dc.Plugin.csproj");
    var panelCodePath = Path.Combine(root, "src", "YiZhiMao.Dc.Plugin", "Views", "ShortcutPanelView.xaml.cs");

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
    Assert(panelCode.Contains("typeof(ShortcutPanelView).Assembly.Location", StringComparison.Ordinal) &&
           panelCode.Contains("HardwareLibraryService.EnsureDefaultFolders(pluginDirectory)",
               StringComparison.Ordinal),
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
            VectorDpi = 2400,
            Margin = 3.5,
            Paper = "ISO_A3"
        };

        store.Save(settings);
        var loaded = store.Load();
        Assert(loaded.PlotTransparency, "打印透明度未持久化。");
        Assert(loaded.PdfOutputMode == PdfOutputMode.SeparateAndMerged, "PDF输出模式未持久化。");
        Assert(loaded.VectorDpi == 2400 && Math.Abs(loaded.Margin - 3.5) < 0.000001, "打印数值设置未持久化。");
        Assert(loaded.Paper == "ISO_A3", "纸张设置未持久化。");

        loaded.VectorDpi = 0;
        AssertThrows<InvalidOperationException>(() => store.Save(loaded), "非法分辨率未被拒绝。");
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

static void PdfToolScansAndMergesInOrder()
{
    var root = Path.Combine(Path.GetTempPath(), $"YiZhiMaoDcTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var first = Path.Combine(root, "A.pdf");
        var second = Path.Combine(root, "B.pdf");
        CreatePdf(first, 1);
        CreatePdf(second, 2);

        var scanned = PdfToolService.Scan(root);
        Assert(scanned.Count == 2, "PDF扫描数量不正确。");
        Assert(scanned[0].FileName == "A.pdf" && scanned[0].PageCount == 1, "PDF扫描排序或页数不正确。");
        Assert(scanned[1].FileName == "B.pdf" && scanned[1].PageCount == 2, "PDF扫描页数不正确。");

        var merged = Path.Combine(root, "merged.pdf");
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
    Assert(File.Exists(commandsPath) && File.Exists(panelPath) && File.Exists(printWindowPath) && File.Exists(pdfWindowPath),
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

    Assert(commands.Contains("\"D1\")", StringComparison.Ordinal) &&
           commands.Contains("\"D2\")", StringComparison.Ordinal) &&
           commands.Contains("\"D3\")", StringComparison.Ordinal) &&
           commands.Contains("\"D4\")", StringComparison.Ordinal) &&
           commands.Contains("\"DY\")", StringComparison.Ordinal), "D1-D4 或 DY 默认快捷键缺失。");

    var panel = File.ReadAllText(panelPath);
    Assert(panel.Contains("Text=\"批量打印\"", StringComparison.Ordinal), "左侧未增加批量打印模块。");
    Assert(panel.Contains("Content=\"PDF 工具\"", StringComparison.Ordinal), "批量打印面板缺少PDF工具入口。");
    Assert(panel.Contains("Content=\"闭合矩形多段线\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"四条直线组成的矩形\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"块参照外框\"", StringComparison.Ordinal), "图框识别选项不完整。");

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
           project.Contains("<BundleAssemblies Include=\"$(TargetDir)*.dll\"", StringComparison.Ordinal),
        "PDFsharp运行依赖未纳入bundle复制规则。");
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
    Assert(printWindow.Contains("toPdf ? CadPlotService.GetDevices() : CadPlotService.GetPaperDevices()",
            StringComparison.Ordinal),
        "批量打印窗口没有按D1/D2用途分离设备列表。");
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

    Assert(commands.Contains("FrameRecognitionService.GetCurrentPaperSpaceLayout", StringComparison.Ordinal) &&
           !commands.Contains("FrameRecognitionService.GetPaperSpaceLayouts", StringComparison.Ordinal),
        "D3/D4仍然枚举全部布局，没有限定为当前布局。");
    Assert(recognition.Contains("GetCurrentPaperSpaceLayout", StringComparison.Ordinal) &&
           recognition.Contains("database.CurrentSpaceId", StringComparison.Ordinal) &&
           recognition.Contains("layout.ModelType", StringComparison.Ordinal),
        "当前布局任务没有从当前空间解析，或没有排除模型空间。");
    Assert(panel.Contains("Content=\"D3 布局打印纸张\"", StringComparison.Ordinal) &&
           panel.Contains("Content=\"D4 布局打印 PDF\"", StringComparison.Ordinal) &&
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
           plotService.Contains("PdfOutputMode.MergedOnly", StringComparison.Ordinal) &&
           plotService.Contains("Path.GetTempPath()", StringComparison.Ordinal) &&
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
    var xaml = File.ReadAllText(xamlPath);
    var document = XDocument.Parse(xaml);
    var resourceKeys = document.Descendants()
        .Attributes()
        .Where(attribute => attribute.Name.LocalName == "Key")
        .Select(attribute => attribute.Value)
        .ToHashSet(StringComparer.Ordinal);
    var references = Regex.Matches(xaml, @"\{StaticResource\s+([^}\s]+)")
        .Select(match => match.Groups[1].Value)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    var undefined = references.Where(reference => !resourceKeys.Contains(reference)).ToArray();

    Assert(undefined.Length == 0,
        $"工具箱XAML引用了未定义资源：{string.Join(", ", undefined)}。" );
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
    var xamlDocument = XDocument.Parse(xaml);
    var textColumns = xamlDocument.Descendants()
        .Where(element => element.Name.LocalName == "DataGridTextColumn")
        .ToList();
    var shortcutDescriptionColumn = textColumns.First(element => (string?)element.Attribute("Header") == "功能说明");
    var defaultColumn = textColumns.Single(element => (string?)element.Attribute("Header") == "默认");
    var customColumn = textColumns.Single(element => (string?)element.Attribute("Header") == "自定义");
    var coreCommandColumn = textColumns.Single(element => (string?)element.Attribute("Header") == "底层命令");
    Assert(xaml.Contains("Text=\"快捷键\"", StringComparison.Ordinal), "左侧缺少快捷键一级模块。") ;
    Assert(xaml.Contains("Text=\"插入图片\"", StringComparison.Ordinal), "左侧缺少插入图片一级模块。") ;
    Assert(xaml.Contains("Content=\"批量插入图片 (OLE)\"", StringComparison.Ordinal) &&
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
    Assert(xaml.Contains("<GridSplitter Grid.Column=\"1\"", StringComparison.Ordinal), "左侧功能区缺少可拖动分隔条。") ;
    Assert(xaml.Contains("TextWrapping=\"Wrap\"", StringComparison.Ordinal), "左侧长功能名称未启用完整换行。") ;
    Assert(xaml.Contains("<Setter Property=\"Foreground\" Value=\"#20242A\" />", StringComparison.Ordinal), "左侧选中项未保留默认深色文字。") ;
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
    Assert(xaml.Contains("CellStyle=\"{StaticResource MutedCellStyle}\"", StringComparison.Ordinal), "底层命令列未使用灰色文字。") ;
    Assert((string?)shortcutDescriptionColumn.Attribute("IsReadOnly") != "True", "功能说明仍不可编辑。") ;
    Assert((string?)defaultColumn.Attribute("IsReadOnly") != "True", "默认快捷键仍不可编辑。") ;
    Assert((string?)customColumn.Attribute("IsReadOnly") != "True", "自定义快捷键仍不可编辑。") ;
    Assert((string?)coreCommandColumn.Attribute("IsReadOnly") == "True", "底层命令未保持只读。") ;
    Assert(xaml.Contains("x:Key=\"GridTextBlockStyle\"", StringComparison.Ordinal) &&
           xaml.Contains("<Setter Property=\"VerticalAlignment\" Value=\"Center\" />", StringComparison.Ordinal),
        "表格文字未显式设置垂直居中。") ;
    Assert(xaml.Contains("<Style x:Key=\"GridCellStyle\"", StringComparison.Ordinal) &&
           xaml.Contains("<Setter Property=\"Foreground\" Value=\"#20242A\" />", StringComparison.Ordinal),
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
    Assert(imageCommands.Contains("[ShortcutCommand(\"插入图片\", \"批量插入图片\", \"OLE\")]", StringComparison.Ordinal),
        "批量图片功能未登记默认OLE快捷键。") ;
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
