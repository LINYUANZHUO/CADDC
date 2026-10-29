namespace YiZhiMao.Dc.Core;

public static class CadCommandCatalog
{
    private static readonly IReadOnlyList<CadCommandItem> Commands = Array.AsReadOnly<CadCommandItem>(
    [
        new("修改命令", "AR", "ARRAY", "阵列"),
        new("修改命令", "BR", "BREAK", "打断"),
        new("修改命令", "CHA", "CHAMFER", "倒角"),
        new("修改命令", "CO", "COPY", "复制"),
        new("修改命令", "ED", "DDEDIT", "修改文字"),
        new("修改命令", "E", "ERASE", "删除"),
        new("修改命令", "X", "EXPLODE", "分解"),
        new("修改命令", "EX", "EXTEND", "延伸"),
        new("修改命令", "F", "FILLET", "圆角"),
        new("修改命令", "LEN", "LENGTHEN", "拉长"),
        new("修改命令", "MI", "MIRROR", "镜像"),
        new("修改命令", "M", "MOVE", "移动"),
        new("修改命令", "O", "OFFSET", "偏移"),
        new("修改命令", "PE", "PEDIT", "多段线编辑"),
        new("修改命令", "RO", "ROTATE", "旋转"),
        new("修改命令", "SC", "SCALE", "缩放"),
        new("修改命令", "S", "STRETCH", "拉伸"),
        new("修改命令", "TR", "TRIM", "修剪"),

        new("对象特性", "AL", "ALIGN", "对齐"),
        new("对象特性", "AA", "AREA", "面积"),
        new("对象特性", "ATT", "ATTDEF", "属性定义"),
        new("对象特性", "ATE", "ATTEDIT", "编辑属性"),
        new("对象特性", "BO", "BOUNDARY", "边界创建"),
        new("对象特性", "COL", "COLOR", "设置颜色"),
        new("对象特性", "DI", "DIST", "距离"),
        new("对象特性", "LA", "LAYER", "图层操作"),
        new("对象特性", "LT", "LINETYPE", "线型"),
        new("对象特性", "LI", "LIST", "图形数据信息"),
        new("对象特性", "LTS", "LTSCALE", "线型比例"),
        new("对象特性", "LW", "LWEIGHT", "线宽"),
        new("对象特性", "MA", "MATCHPROP", "属性匹配"),
        new("对象特性", "OP", "OPTIONS", "CAD 设置"),
        new("对象特性", "OS", "OSNAP", "设置捕捉模式"),
        new("对象特性", "PLOT", "PRINT", "打印"),
        new("对象特性", "PU", "PURGE", "清理未使用项目"),
        new("对象特性", "RE", "REGEN", "重新生成"),
        new("对象特性", "REN", "RENAME", "重命名"),
        new("对象特性", "ST", "STYLE", "文字样式"),
        new("对象特性", "UN", "UNITS", "图形单位"),

        new("尺寸标注", "LE", "QLEADER", "引线标注"),
        new("尺寸标注", "DAL", "DIMALIGNED", "对齐标注"),
        new("尺寸标注", "DAN", "DIMANGULAR", "角度标注"),
        new("尺寸标注", "DAR", "DIMARC", "弧长标注"),
        new("尺寸标注", "DBA", "DIMBASELINE", "基线标注"),
        new("尺寸标注", "DCE", "DIMCENTER", "中心标记"),
        new("尺寸标注", "DCO", "DIMCONTINUE", "连续标注"),
        new("尺寸标注", "DDI", "DIMDIAMETER", "直径标注"),
        new("尺寸标注", "DED", "DIMEDIT", "编辑标注"),
        new("尺寸标注", "DJO", "DIMJOGGED", "折弯标注"),
        new("尺寸标注", "DLI", "DIMLINEAR", "线性标注"),
        new("尺寸标注", "DOR", "DIMORDINATE", "坐标标注"),
        new("尺寸标注", "DRA", "DIMRADIUS", "半径标注"),
        new("尺寸标注", "D", "DIMSTYLE", "标注样式"),
        new("尺寸标注", "QD", "QDIM", "快速标注"),

        new("常用 Ctrl 快捷键", "Ctrl+2", "ADCENTER", "设计中心"),
        new("常用 Ctrl 快捷键", "Ctrl+C", "COPYCLIP", "复制"),
        new("常用 Ctrl 快捷键", "Ctrl+O", "OPEN", "打开文件"),
        new("常用 Ctrl 快捷键", "Ctrl+F", "OSNAP", "对象捕捉"),
        new("常用 Ctrl 快捷键", "Ctrl+V", "PASTECLIP", "粘贴"),
        new("常用 Ctrl 快捷键", "Ctrl+1", "PROPERTIES", "特性"),
        new("常用 Ctrl 快捷键", "Ctrl+S", "QSAVE", "保存文件"),
        new("常用 Ctrl 快捷键", "Ctrl+Z", "UNDO", "放弃上一步"),

        new("视图缩放与捕捉点", "INT", "OSNAP", "交点"),
        new("视图缩放与捕捉点", "END", "OSNAP", "端点"),
        new("视图缩放与捕捉点", "PER", "OSNAP", "垂足"),
        new("视图缩放与捕捉点", "MTP", "OSNAP", "两点之间的中点"),
        new("视图缩放与捕捉点", "CEN", "OSNAP", "圆心"),
        new("视图缩放与捕捉点", "Z+E", "ZOOM EXTENTS", "显示全图"),
        new("视图缩放与捕捉点", "Z+W", "ZOOM WINDOW", "窗口缩放"),

        new("绘图命令", "A", "ARC", "圆弧"),
        new("绘图命令", "H", "BHATCH", "填充"),
        new("绘图命令", "B", "BLOCK", "创建块"),
        new("绘图命令", "C", "CIRCLE", "圆"),
        new("绘图命令", "DIV", "DIVIDE", "定数等分"),
        new("绘图命令", "DO", "DONUT", "圆环"),
        new("绘图命令", "EL", "ELLIPSE", "椭圆"),
        new("绘图命令", "I", "INSERT", "插入块"),
        new("绘图命令", "L", "LINE", "直线"),
        new("绘图命令", "ME", "MEASURE", "定距等分"),
        new("绘图命令", "ML", "MLINE", "多线"),
        new("绘图命令", "MT", "MTEXT", "多行文字"),
        new("绘图命令", "P", "POINT", "点"),
        new("绘图命令", "PL", "PLINE", "多段线"),
        new("绘图命令", "POL", "POLYGON", "正多边形"),
        new("绘图命令", "REC", "RECTANG", "矩形"),
        new("绘图命令", "REG", "REGION", "面域"),
        new("绘图命令", "SPL", "SPLINE", "样条曲线"),
        new("绘图命令", "W", "WBLOCK", "写块"),
        new("绘图命令", "XL", "XLINE", "构造线"),

        new("高级拓展工具", "AUTOCONS", "AUTOCONSTRAIN", "自动约束"),
        new("高级拓展工具", "DCON", "DIMCONSTRAINT", "尺寸约束"),
        new("高级拓展工具", "TXTEXP", "TXTEXP", "文字分解"),
        new("高级拓展工具", "TXT2MTXT", "TXT2MTXT", "单行文字转多行"),
        new("高级拓展工具", "ARCTEXT", "ARCTEXT", "弧形文字"),
        new("高级拓展工具", "TCOUNT", "TCOUNT", "文字递增"),
        new("高级拓展工具", "FLATTEN", "FLATTEN", "对象展平"),
        new("高级拓展工具", "SUPERHATCH", "SUPERHATCH", "超级填充"),
        new("高级拓展工具", "GCON", "GEOMCONSTRAINT", "几何约束")
    ]);

    public static IReadOnlyList<CadCommandItem> GetAll()
    {
        return Commands;
    }

    public static bool Matches(CadCommandItem item, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var normalized = keyword.Trim();
        return Contains(item.Category, normalized) ||
               Contains(item.Shortcut, normalized) ||
               Contains(item.FullCommand, normalized) ||
               Contains(item.Description, normalized);
    }

    private static bool Contains(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
