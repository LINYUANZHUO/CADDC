using System.Collections.Concurrent;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using YiZhiMao.Dc.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

internal static class ShxFontSyncRuntime
{
    private static readonly HashSet<Database> AttachedDatabases = [];
    private static readonly ConcurrentDictionary<string, byte> PendingDrawingFiles =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> PendingMessages = new();
    private static readonly SemaphoreSlim SyncGate = new(1, 1);
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        foreach (Document document in AcadApplication.DocumentManager)
        {
            Attach(document.Database);
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
        foreach (var database in AttachedDatabases.ToArray())
        {
            Detach(database);
        }
    }

    public static async Task<ShxFontSyncReport> ExportActiveDocumentAsync()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument
                       ?? throw new InvalidOperationException("当前没有可用图纸。");
        var drawingFile = document.Database.Filename;
        if (!IsFormalDrawingFile(drawingFile))
        {
            throw new InvalidOperationException("请先保存当前图纸，再导出本图 SHX。");
        }

        IReadOnlyCollection<string> fontFiles;
        using (document.LockDocument())
        {
            fontFiles = CollectReferencedShxFiles(document.Database);
        }

        return await SynchronizeAsync(drawingFile, fontFiles).ConfigureAwait(false);
    }

    public static string FormatReport(ShxFontSyncReport report) => report.Items.Count == 0
        ? "本图未引用需要同步的 SHX 文件。"
        : $"SHX 同步完成：复制 {report.CopiedCount}，已存在 {report.ExistingCount}，" +
          $"缺失 {report.MissingCount}，冲突 {report.ConflictCount}，失败 {report.FailedCount}。";

    private static void OnDocumentCreated(object sender, DocumentCollectionEventArgs e) =>
        Attach(e.Document.Database);

    private static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e) =>
        Detach(e.Document.Database);

    private static void Attach(Database database)
    {
        if (AttachedDatabases.Add(database))
        {
            database.SaveComplete += OnSaveComplete;
        }
    }

    private static void Detach(Database database)
    {
        if (AttachedDatabases.Remove(database))
        {
            database.SaveComplete -= OnSaveComplete;
        }
    }

    private static void OnSaveComplete(object sender, DatabaseIOEventArgs e)
    {
        if (!ToolboxSettingsRuntime.Settings.SyncReferencedShxOnSave ||
            sender is not Database database ||
            !IsFormalDrawingFile(e.FileName))
        {
            return;
        }

        try
        {
            QueueAutomaticSync(e.FileName, CollectReferencedShxFiles(database));
        }
        catch (System.Exception exception)
        {
            PendingMessages.Enqueue($"一只猫dc SHX 同步未执行：{exception.Message}");
        }
    }

    private static void QueueAutomaticSync(string drawingFile, IReadOnlyCollection<string> fontFiles)
    {
        var fullPath = Path.GetFullPath(drawingFile);
        if (!PendingDrawingFiles.TryAdd(fullPath, 0))
        {
            return;
        }

        _ = RunAutomaticSyncAsync(fullPath, fontFiles);
    }

    private static async Task RunAutomaticSyncAsync(
        string drawingFile,
        IReadOnlyCollection<string> fontFiles)
    {
        try
        {
            var report = await SynchronizeAsync(drawingFile, fontFiles).ConfigureAwait(false);
            PendingMessages.Enqueue(FormatReport(report));
        }
        catch (System.Exception exception)
        {
            PendingMessages.Enqueue($"一只猫dc SHX 同步失败：{exception.Message}");
        }
        finally
        {
            PendingDrawingFiles.TryRemove(drawingFile, out _);
        }
    }

    private static async Task<ShxFontSyncReport> SynchronizeAsync(
        string drawingFile,
        IReadOnlyCollection<string> fontFiles)
    {
        var drawingDirectory = Path.GetDirectoryName(Path.GetFullPath(drawingFile))
                               ?? throw new InvalidOperationException("无法确定当前图纸目录。");
        var fontLibrary = Path.Combine(PluginResourceLocator.GetPluginRoot(), "Modules", "Fonts");

        await SyncGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
                    ShxFontSyncService.Sync(fontLibrary, drawingDirectory, fontFiles))
                .ConfigureAwait(false);
        }
        finally
        {
            SyncGate.Release();
        }
    }

    private static IReadOnlyCollection<string> CollectReferencedShxFiles(Database database)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var textStyles = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
        foreach (ObjectId styleId in textStyles)
        {
            CollectTextStyleFiles(transaction, styleId, files);
        }

        var linetypes = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        foreach (ObjectId linetypeId in linetypes)
        {
            var linetype = (LinetypeTableRecord)transaction.GetObject(linetypeId, OpenMode.ForRead);
            for (var index = 0; index < linetype.NumDashes; index++)
            {
                var styleId = linetype.ShapeStyleAt(index);
                if (!styleId.IsNull && styleId.IsValid)
                {
                    CollectTextStyleFiles(transaction, styleId, files);
                }
            }
        }

        transaction.Commit();
        return files.ToArray();
    }

    private static void CollectTextStyleFiles(
        Transaction transaction,
        ObjectId styleId,
        ISet<string> files)
    {
        if (transaction.GetObject(styleId, OpenMode.ForRead, openErased: false) is not TextStyleTableRecord style)
        {
            return;
        }

        AddShxFile(style.FileName, files);
        AddShxFile(style.BigFontFileName, files);
    }

    private static void AddShxFile(string? fileName, ISet<string> files)
    {
        if (!string.IsNullOrWhiteSpace(fileName) &&
            string.Equals(Path.GetExtension(fileName), ".shx", StringComparison.OrdinalIgnoreCase))
        {
            files.Add(fileName!);
        }
    }

    private static bool IsFormalDrawingFile(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        Path.IsPathRooted(fileName) &&
        string.Equals(Path.GetExtension(fileName), ".dwg", StringComparison.OrdinalIgnoreCase);

    private static void OnIdle(object? sender, EventArgs e)
    {
        var editor = AcadApplication.DocumentManager.MdiActiveDocument?.Editor;
        if (editor is null)
        {
            return;
        }

        while (PendingMessages.TryDequeue(out var message))
        {
            editor.WriteMessage($"\n{message}");
        }
    }
}
