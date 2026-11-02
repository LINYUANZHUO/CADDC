using System.Security.Cryptography;
using System.Text;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using YiZhiMao.Dc.Plugin.Views;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

public static class SuperLibraryCommands
{
    [CommandMethod("DC_CMD_SUPER_LIBRARY", CommandFlags.Modal)]
    [ShortcutCommand("超级图库", "打开超级图库", "DCTK")]
    public static void ShowLibrary()
    {
        if (ModulePreferencesRuntime.EnsureEnabled(ToolboxFeatureModule.SuperLibrary))
        {
            SuperLibraryWindowManager.Show();
        }
    }

    [CommandMethod("DC_CMD_SUPER_LIBRARY_INSERT", CommandFlags.Modal)]
    [ShortcutCommand("超级图库", "插入当前图库项目", "DCTKI")]
    public static void InsertSelectedItem()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !ModulePreferencesRuntime.EnsureEnabled(ToolboxFeatureModule.SuperLibrary, document))
        {
            return;
        }

        var itemPath = SuperLibraryRuntime.SelectedItemPath;
        if (itemPath is null ||
            string.IsNullOrWhiteSpace(itemPath) ||
            !File.Exists(itemPath) ||
            !itemPath.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
        {
            document.Editor.WriteMessage("\n[一只猫dc] 请先在超级图库中单击一个有效的 DWG 项目。");
            return;
        }

        try
        {
            var blockId = ImportDrawingAsBlock(document.Database, itemPath);
            var reference = new BlockReference(Point3d.Origin, blockId);
            var jig = new BlockPlacementJig(reference, Path.GetFileNameWithoutExtension(itemPath));
            var result = document.Editor.Drag(jig);
            if (result.Status != PromptStatus.OK)
            {
                reference.Dispose();
                document.Editor.WriteMessage("\n[一只猫dc] 已取消插入图库项目。");
                return;
            }

            using var transaction = document.Database.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)transaction.GetObject(
                document.Database.CurrentSpaceId,
                OpenMode.ForWrite);
            space.AppendEntity(reference);
            transaction.AddNewlyCreatedDBObject(reference, true);
            transaction.Commit();
            document.Editor.WriteMessage($"\n[一只猫dc] 已插入图库项目：{Path.GetFileNameWithoutExtension(itemPath)}。");
        }
        catch (System.Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 图库项目插入失败：{exception.Message}");
        }
    }

    [CommandMethod("DC_CMD_SUPER_LIBRARY_SAVE", CommandFlags.Modal)]
    [ShortcutCommand("超级图库", "保存选中图形到图库", "DCBCT")]
    public static void SaveSelectionToLibrary()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !ModulePreferencesRuntime.EnsureEnabled(ToolboxFeatureModule.SuperLibrary, document))
        {
            return;
        }

        var editor = document.Editor;
        var categoryName = SuperLibraryRuntime.SelectedCategoryName;
        if (categoryName is null || categoryName.Trim().Length == 0)
        {
            editor.WriteMessage("\n[一只猫dc] 请先在超级图库中选择保存分类。");
            return;
        }

        var selection = editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n请选择需要保存到超级图库的图形："
        });
        if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
        {
            editor.WriteMessage("\n[一只猫dc] 未选择可保存的图形。");
            return;
        }

        var basePoint = editor.GetPoint("\n请指定图库组件插入基点：");
        if (basePoint.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[一只猫dc] 已取消保存图库项目。");
            return;
        }

        var nameOptions = new PromptStringOptions("\n请输入图库名称：")
        {
            AllowSpaces = true
        };
        var nameResult = editor.GetString(nameOptions);
        if (nameResult.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(nameResult.StringResult))
        {
            editor.WriteMessage("\n[一只猫dc] 图库名称不能为空。");
            return;
        }

        try
        {
            var root = SuperLibraryRuntime.RootDirectory;
            var outputPath = SuperLibraryCatalog.ResolveSavePath(root, categoryName, nameResult.StringResult);
            if (File.Exists(outputPath))
            {
                editor.WriteMessage("\n[一只猫dc] 同名图库项目已存在，请换一个名称。");
                return;
            }

            using var outputDatabase = new Database(true, true);
            document.Database.Wblock(
                outputDatabase,
                new ObjectIdCollection(selection.Value.GetObjectIds()),
                basePoint.Value,
                DuplicateRecordCloning.Ignore);
            outputDatabase.SaveAs(outputPath, DwgVersion.Current);
            SuperLibraryWindowManager.Refresh();
            editor.WriteMessage($"\n[一只猫dc] 已保存图库项目：{outputPath}");
        }
        catch (System.Exception exception)
        {
            editor.WriteMessage($"\n[一只猫dc] 保存图库项目失败：{exception.Message}");
        }
    }

    private static ObjectId ImportDrawingAsBlock(Database targetDatabase, string filePath)
    {
        var blockName = CreateBlockName(filePath);
        using (var transaction = targetDatabase.TransactionManager.StartOpenCloseTransaction())
        {
            var blockTable = (BlockTable)transaction.GetObject(targetDatabase.BlockTableId, OpenMode.ForRead);
            if (blockTable.Has(blockName))
            {
                return blockTable[blockName];
            }
        }

        using var sourceDatabase = new Database(false, true);
        sourceDatabase.ReadDwgFile(filePath, FileOpenMode.OpenForReadAndAllShare, true, null);
        sourceDatabase.CloseInput(true);
        return targetDatabase.Insert(blockName, sourceDatabase, true);
    }

    private static string CreateBlockName(string filePath)
    {
        var sourceName = Path.GetFileNameWithoutExtension(filePath);
        var safeName = new string(sourceName
            .Select(character => char.IsLetterOrDigit(character) || character is '_' or '-'
                ? character
                : '_')
            .Take(40)
            .ToArray());
        using var sha256 = SHA256.Create();
        var pathHash = BitConverter.ToString(
                sha256.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(filePath))),
                0,
                6)
            .Replace("-", string.Empty);
        return $"DC_LIBRARY_{(string.IsNullOrEmpty(safeName) ? "ITEM" : safeName)}_{pathHash}_{File.GetLastWriteTimeUtc(filePath).Ticks:X}";
    }

    private sealed class BlockPlacementJig(BlockReference reference, string itemName) : EntityJig(reference)
    {
        private Point3d position = Point3d.Origin;

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions($"\n移动鼠标放置“{itemName}”，单击确认：")
            {
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.NullResponseAccepted
            };
            var result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(position) <= Tolerance.Global.EqualPoint)
            {
                return SamplerStatus.NoChange;
            }

            position = result.Value;
            return SamplerStatus.OK;
        }

        protected override bool Update()
        {
            reference.Position = position;
            return true;
        }
    }
}

internal static class SuperLibraryRuntime
{
    public static string RootDirectory =>
        SuperLibraryCatalog.EnsureRoot(PluginResourceLocator.GetPluginRoot());

    public static string? SelectedItemPath { get; private set; }

    public static string? SelectedCategoryName { get; private set; }

    public static void SelectCategory(string categoryName)
    {
        SelectedCategoryName = categoryName;
    }

    public static void SelectItem(SuperLibraryItem item)
    {
        SelectedCategoryName = item.CategoryName;
        SelectedItemPath = item.DwgPath;
    }
}
