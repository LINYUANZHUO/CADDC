using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using YiZhiMao.Dc.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

internal static class MissingShxReplacementRuntime
{
    private static readonly HashSet<Database> PendingDatabases = [];
    private static readonly HashSet<Database> ScannedDatabases = [];
    private static bool initialized;
    private static bool scanning;
    private static bool fontLibraryReady;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        if (ToolboxSettingsRuntime.Settings.AutoReplaceMissingShx)
        {
            RefreshFontLibraryAvailability();
            foreach (Document document in AcadApplication.DocumentManager)
            {
                Queue(document, force: false);
            }
        }

        AcadApplication.DocumentManager.DocumentCreated += OnDocumentCreated;
        AcadApplication.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
        AcadApplication.Idle += OnIdle;
    }

    public static void Dispose()
    {
        if (!initialized)
        {
            return;
        }

        initialized = false;
        AcadApplication.DocumentManager.DocumentCreated -= OnDocumentCreated;
        AcadApplication.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
        AcadApplication.Idle -= OnIdle;
        PendingDatabases.Clear();
        ScannedDatabases.Clear();
    }

    public static void QueueActiveDocument()
    {
        if (!ToolboxSettingsRuntime.Settings.AutoReplaceMissingShx)
        {
            return;
        }

        RefreshFontLibraryAvailability();
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is not null)
        {
            Queue(document, force: true);
        }
    }

    private static void OnDocumentCreated(object sender, DocumentCollectionEventArgs e) =>
        TryQueue(e.Document, force: false);

    private static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
    {
        try
        {
            var database = e.Document?.Database;
            if (database is null)
            {
                return;
            }

            PendingDatabases.Remove(database);
            ScannedDatabases.Remove(database);
        }
        catch (System.Exception)
        {
            // AutoCAD 文档事件不能让托管异常穿过原生回调边界。
        }
    }

    private static void TryQueue(Document? document, bool force)
    {
        try
        {
            Queue(document, force);
        }
        catch (System.Exception)
        {
            // AutoCAD 文档事件不能让托管异常穿过原生回调边界。
        }
    }

    private static void Queue(Document? document, bool force)
    {
        if (!ToolboxSettingsRuntime.Settings.AutoReplaceMissingShx ||
            document?.Database is not Database database)
        {
            return;
        }

        if (force)
        {
            ScannedDatabases.Remove(database);
        }

        if (!ScannedDatabases.Contains(database))
        {
            PendingDatabases.Add(database);
        }
    }

    private static void OnIdle(object? sender, EventArgs e)
    {
        if (scanning)
        {
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !PendingDatabases.Remove(document.Database))
        {
            return;
        }

        if (!ToolboxSettingsRuntime.Settings.AutoReplaceMissingShx)
        {
            return;
        }

        scanning = true;
        try
        {
            var report = Scan(document);
            ScannedDatabases.Add(document.Database);
            if (report.HasResult)
            {
                document.Editor.WriteMessage($"\n{report.FormatMessage()}");
            }
        }
        catch (System.Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 缺失 SHX 检查失败：{exception.Message}");
        }
        finally
        {
            scanning = false;
        }
    }

    private static MissingShxScanReport Scan(Document document)
    {
        var database = document.Database;
        var fontLibrary = Path.Combine(PluginResourceLocator.GetPluginRoot(), "Modules", "Fonts");
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var libraryFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unresolvedShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unresolvedTextFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (document.LockDocument())
        using (var transaction = database.TransactionManager.StartTransaction())
        {
            var shapeStyleIds = CollectShapeStyleIds(database, transaction);
            var textStyles = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
            foreach (ObjectId styleId in textStyles)
            {
                var style = (TextStyleTableRecord)transaction.GetObject(styleId, OpenMode.ForRead);
                if (shapeStyleIds.Contains(styleId))
                {
                    var shapeReplacement = ChooseShapeLibraryReference(
                        style.FileName,
                        database,
                        fontLibrary,
                        libraryFonts,
                        unresolvedShapes);
                    if (shapeReplacement is not null)
                    {
                        var originalFileName = style.FileName;
                        style.UpgradeOpen();
                        style.FileName = shapeReplacement;
                        replacements[originalFileName] = shapeReplacement;
                    }

                    continue;
                }

                var normalReplacement = ChooseReplacement(
                    style.FileName,
                    ShxReferenceKind.TextFont,
                    database,
                    fontLibrary,
                    libraryFonts,
                    unresolvedTextFonts);
                var bigFontReplacement = ChooseReplacement(
                    style.BigFontFileName,
                    ShxReferenceKind.BigFont,
                    database,
                    fontLibrary,
                    libraryFonts,
                    unresolvedTextFonts);
                if (normalReplacement is null && bigFontReplacement is null)
                {
                    continue;
                }

                style.UpgradeOpen();
                if (normalReplacement is not null)
                {
                    replacements[style.FileName] = normalReplacement;
                    style.FileName = normalReplacement;
                }

                if (bigFontReplacement is not null)
                {
                    replacements[style.BigFontFileName] = bigFontReplacement;
                    style.BigFontFileName = bigFontReplacement;
                }
            }

            transaction.Commit();
        }

        if (replacements.Count > 0)
        {
            RecordMappings(replacements);
        }

        return new MissingShxScanReport(
            replacements,
            libraryFonts,
            unresolvedShapes,
            unresolvedTextFonts);
    }

    private static HashSet<ObjectId> CollectShapeStyleIds(Database database, Transaction transaction)
    {
        var shapeStyleIds = new HashSet<ObjectId>();
        var linetypes = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        foreach (ObjectId linetypeId in linetypes)
        {
            var linetype = (LinetypeTableRecord)transaction.GetObject(linetypeId, OpenMode.ForRead);
            for (var index = 0; index < linetype.NumDashes; index++)
            {
                var styleId = linetype.ShapeStyleAt(index);
                if (!styleId.IsNull && styleId.IsValid)
                {
                    shapeStyleIds.Add(styleId);
                }
            }
        }

        return shapeStyleIds;
    }

    private static string? ChooseShapeLibraryReference(
        string? fileName,
        Database database,
        string fontLibrary,
        ISet<string> libraryFonts,
        ISet<string> unresolvedShapes)
    {
        var decision = MissingShxReplacementPolicy.Decide(
            fileName,
            ShxReferenceKind.ShapeFile,
            IsAvailable(fileName, database),
            fontLibraryReady && IsInLibrary(fileName, fontLibrary));
        if (decision.Action == MissingShxAction.UseLibrary)
        {
            libraryFonts.Add(Path.GetFileName(fileName!));
            return Path.Combine(fontLibrary, decision.ReplacementFile!);
        }

        if (decision.Action == MissingShxAction.ReportOnly)
        {
            unresolvedShapes.Add(Path.GetFileName(fileName!));
        }

        return null;
    }

    private static string? ChooseReplacement(
        string? fileName,
        ShxReferenceKind kind,
        Database database,
        string fontLibrary,
        ISet<string> libraryFonts,
        ISet<string> unresolvedTextFonts)
    {
        var decision = MissingShxReplacementPolicy.Decide(
            fileName,
            kind,
            IsAvailable(fileName, database),
            fontLibraryReady && IsInLibrary(fileName, fontLibrary));
        if (decision.Action == MissingShxAction.UseLibrary)
        {
            libraryFonts.Add(Path.GetFileName(fileName!));
            return Path.Combine(fontLibrary, decision.ReplacementFile!);
        }

        if (decision.Action != MissingShxAction.Replace ||
            !IsAvailable(decision.ReplacementFile, database))
        {
            if (decision.Action == MissingShxAction.Replace)
            {
                unresolvedTextFonts.Add(Path.GetFileName(fileName!));
            }

            return null;
        }

        return decision.ReplacementFile;
    }

    private static bool IsAvailable(string? fileName, Database database)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return true;
        }

        if (Path.IsPathRooted(fileName) && File.Exists(fileName))
        {
            return true;
        }

        try
        {
            var resolved = HostApplicationServices.Current.FindFile(
                fileName,
                database,
                FindFileHint.Default);
            return !string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved);
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private static bool IsInLibrary(string? fileName, string fontLibrary)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var name = Path.GetFileName(fileName);
        return !string.IsNullOrWhiteSpace(name) && File.Exists(Path.Combine(fontLibrary, name));
    }

    private static void RefreshFontLibraryAvailability()
    {
        var fontLibrary = Path.Combine(PluginResourceLocator.GetPluginRoot(), "Modules", "Fonts");
        fontLibraryReady = Directory.Exists(fontLibrary);
    }

    private static void RecordMappings(IReadOnlyDictionary<string, string> replacements)
    {
        ToolboxSettingsRuntime.Update(settings =>
        {
            foreach (var pair in replacements)
            {
                var existingKey = settings.MissingShxReplacementMappings.Keys.FirstOrDefault(key =>
                    string.Equals(key, pair.Key, StringComparison.OrdinalIgnoreCase));
                if (existingKey is not null && !string.Equals(existingKey, pair.Key, StringComparison.Ordinal))
                {
                    settings.MissingShxReplacementMappings.Remove(existingKey);
                }

                settings.MissingShxReplacementMappings[pair.Key] = pair.Value;
            }
        });
    }

    private sealed record MissingShxScanReport(
        IReadOnlyDictionary<string, string> Replacements,
        IReadOnlyCollection<string> LibraryFonts,
        IReadOnlyCollection<string> UnresolvedShapes,
        IReadOnlyCollection<string> UnresolvedTextFonts)
    {
        public bool HasResult =>
            Replacements.Count > 0 ||
            LibraryFonts.Count > 0 ||
            UnresolvedShapes.Count > 0 ||
            UnresolvedTextFonts.Count > 0;

        public string FormatMessage()
        {
            var parts = new List<string>();
            if (Replacements.Count > 0)
            {
                parts.Add("已替换 " + string.Join(", ", Replacements.Select(pair => $"{pair.Key}→{pair.Value}")));
            }

            if (LibraryFonts.Count > 0)
            {
                parts.Add("字体库已匹配 " + string.Join(", ", LibraryFonts));
            }

            if (UnresolvedShapes.Count > 0)
            {
                parts.Add("未替换形文件 " + string.Join(", ", UnresolvedShapes));
            }

            if (UnresolvedTextFonts.Count > 0)
            {
                parts.Add("仍缺少文字字体 " + string.Join(", ", UnresolvedTextFonts));
            }

            var suffix = Replacements.Count > 0 ? "；请检查文字后保存图纸。" : "。";
            return "[一只猫dc] 缺失 SHX 处理：" + string.Join("；", parts) + suffix;
        }
    }
}
