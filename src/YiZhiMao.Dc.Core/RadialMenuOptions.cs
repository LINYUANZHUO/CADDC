using System.Text.Json;

namespace YiZhiMao.Dc.Core;

public sealed class RadialMenuItem
{
    public int Ring { get; set; }

    public int Position { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CommandAlias { get; set; } = string.Empty;

    public string PositionLabel => $"{(Ring == 3 ? "三" : Ring)}圈-{Position}";

    public RadialMenuItem Clone() => new()
    {
        Ring = Ring,
        Position = Position,
        Name = Name,
        CommandAlias = CommandAlias
    };
}

public sealed class RadialMenuOptions
{
    public bool Enabled { get; set; } = true;

    public bool AutoCloseAfterCommand { get; set; } = true;

    public bool ShowThirdRing { get; set; } = true;

    public double Radius { get; set; } = 200;

    public double Opacity { get; set; } = 1;

    public string Skin { get; set; } = "科技深蓝 (Tech Blue)";

    public List<RadialMenuItem> Items { get; set; } = [];

    public static RadialMenuOptions CreateDefault() => new()
    {
        Items =
        [
            Item(1, 1, "快速拉伸", "KS"), Item(1, 2, "超级拉伸", "SS"),
            Item(1, 3, "单元柜", "DG"), Item(1, 4, "吊柜", "WG"),
            Item(1, 5, "框架柜", "KJ"), Item(1, 6, "切角柜", "JG"),
            Item(1, 7, "生成门板", "DCMB"), Item(1, 8, "均分板件", "DCFC"),

            Item(2, 1, "单线变双", "DCZ2"), Item(2, 2, "单线变三", "DCZ3"),
            Item(2, 3, "轮廓矩形", "LK"), Item(2, 4, "线转多段", "XD"),
            Item(2, 5, "线去重", "XQC"), Item(2, 6, "多线连接", "XLJ"),
            Item(2, 7, "线打断", "XDD"), Item(2, 8, "交点切断", "AB"),
            Item(2, 9, "一键隐藏五金", "TCWJ"), Item(2, 10, "一键隐藏门板", "TCMB"),
            Item(2, 11, "一键隐藏标注", "TCBZ"), Item(2, 12, "显示图层", "TCXS"),
            Item(2, 13, "显示修复", "TXF"), Item(2, 14, "批量打印", "DCDY"),
            Item(2, 15, "绘制背板", "DCBB"), Item(2, 16, "生成散板", "DCCB"),

            Item(3, 1, "直线", "L"), Item(3, 2, "多段线", "PL"),
            Item(3, 3, "圆", "C"), Item(3, 4, "矩形", "REC"),
            Item(3, 5, "移动", "M"), Item(3, 6, "复制", "CO"),
            Item(3, 7, "旋转", "RO"), Item(3, 8, "缩放", "SC"),
            Item(3, 9, "修剪", "TR"), Item(3, 10, "延伸", "EX"),
            Item(3, 11, "偏移", "O"), Item(3, 12, "镜像", "MI"),
            Item(3, 13, "圆角", "F"), Item(3, 14, "倒角", "CHA"),
            Item(3, 15, "删除", "E"), Item(3, 16, "分解", "X"),
            Item(3, 17, "图案填充", "H"), Item(3, 18, "线性标注", "DLI"),
            Item(3, 19, "对齐标注", "DAL"), Item(3, 20, "多行文字", "MT"),
            Item(3, 21, "特性匹配", "MA"), Item(3, 22, "图层特性", "LA"),
            Item(3, 23, "圆弧", "A"), Item(3, 24, "打断", "BR")
        ]
    };

    public RadialMenuOptions Clone() => new()
    {
        Enabled = Enabled,
        AutoCloseAfterCommand = AutoCloseAfterCommand,
        ShowThirdRing = ShowThirdRing,
        Radius = Radius,
        Opacity = Opacity,
        Skin = Skin,
        Items = Items.Select(item => item.Clone()).ToList()
    };

    public void Validate()
    {
        if (!double.IsFinite(Radius) || Radius < 120 || Radius > 320)
        {
            throw new InvalidOperationException("圆盘大小必须在 120 到 320 之间。");
        }

        if (!double.IsFinite(Opacity) || Opacity < 0.35 || Opacity > 1)
        {
            throw new InvalidOperationException("圆盘透明度必须在 0.35 到 1.0 之间。");
        }

        Items ??= [];
        if (Items.Count != 48 || Items.Any(item => item.Ring is < 1 or > 3))
        {
            throw new InvalidOperationException("圆盘必须包含 48 个有效命令位置。");
        }

        foreach (var (ring, count) in new[] { (1, 8), (2, 16), (3, 24) })
        {
            var items = Items.Where(item => item.Ring == ring).ToArray();
            if (items.Length != count || items.Select(item => item.Position).Distinct().Count() != count ||
                items.Any(item => item.Position < 1 || item.Position > count))
            {
                throw new InvalidOperationException($"圆盘第 {ring} 圈必须包含 {count} 个不重复位置。");
            }
        }

        if (Items.Any(item => string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.CommandAlias)))
        {
            throw new InvalidOperationException("圆盘命令名称和 CAD 快捷键不能为空。");
        }
    }

    private static RadialMenuItem Item(int ring, int position, string name, string commandAlias) => new()
    {
        Ring = ring,
        Position = position,
        Name = name,
        CommandAlias = commandAlias
    };
}

public static class RadialMenuOptionsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static RadialMenuOptions Load(string path)
    {
        if (!File.Exists(path))
        {
            return RadialMenuOptions.CreateDefault();
        }

        try
        {
            var options = JsonSerializer.Deserialize<RadialMenuOptions>(File.ReadAllText(path), JsonOptions)
                ?? RadialMenuOptions.CreateDefault();
            options.Validate();
            return options;
        }
        catch (JsonException)
        {
            return RadialMenuOptions.CreateDefault();
        }
        catch (InvalidOperationException)
        {
            return RadialMenuOptions.CreateDefault();
        }
    }

    public static void Save(string path, RadialMenuOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(options, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }
}

public readonly record struct RadialMenuSlot(int Ring, int Position);

public static class RadialMenuHitTester
{
    public static RadialMenuSlot? HitTest(double x, double y, double radius, bool showThirdRing)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            return null;
        }

        var distance = Math.Sqrt(x * x + y * y);
        var ring = distance <= radius * 0.16
            ? 0
            : distance <= radius * 0.44
                ? 1
                : distance <= radius * 0.70
                    ? 2
                    : distance <= radius && showThirdRing
                        ? 3
                        : 0;
        if (ring == 0)
        {
            return null;
        }

        var count = ring * 8;
        var segmentAngle = 360d / count;
        var angle = Math.Atan2(y, x) * 180 / Math.PI;
        var normalized = (angle + 90 + segmentAngle / 2 + 360) % 360;
        return new RadialMenuSlot(ring, (int)Math.Floor(normalized / segmentAngle) + 1);
    }
}
