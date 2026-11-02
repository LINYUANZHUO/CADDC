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
    public const string DefaultSkin = "橙灰极简 (Orange Slate)";
    public const string CadDarkSkin = "CAD 深色";
    public const string PurpleGraySkin = "紫灰";

    private static readonly HashSet<string> UnsafeExternalCommandAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "KS",
        "SS",
        "LS_CMD_KS_SMART",
        "LS_CMD_SUPER_STRETCH"
    };

    public bool Enabled { get; set; } = true;

    public bool AutoCloseAfterCommand { get; set; } = true;

    public bool ShowThirdRing { get; set; } = true;

    public double Radius { get; set; } = 200;

    public double Opacity { get; set; } = 1;

    public string Skin { get; set; } = DefaultSkin;

    public List<RadialMenuItem> Items { get; set; } = [];

    public static RadialMenuOptions CreateDefault() => new()
    {
        Items =
        [
            Empty(1, 1), Empty(1, 2),
            Empty(1, 3), Empty(1, 4),
            Empty(1, 5), Empty(1, 6),
            Item(1, 7, "生成门板", "DCMB"), Item(1, 8, "均分板件", "DCFC"),

            Item(2, 1, "单线变双", "DC22"), Item(2, 2, "单线变三", "DC33"),
            Empty(2, 3), Empty(2, 4),
            Empty(2, 5), Empty(2, 6),
            Empty(2, 7), Empty(2, 8),
            Item(2, 9, "一键隐藏五金", "DCTCWJ"), Item(2, 10, "一键隐藏门板", "DCTCMB"),
            Item(2, 11, "一键隐藏标注", "DCTCBZ"), Item(2, 12, "显示图层", "DCTCXS"),
            Item(2, 13, "显示修复", "DCTXF"), Item(2, 14, "批量打印", "DCDY"),
            Item(2, 15, "绘制背板", "DCBB"), Item(2, 16, "绘制板件", "DCCB"),

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

    public static bool IsUnsafeExternalCommandAlias(string? commandAlias)
    {
        if (string.IsNullOrWhiteSpace(commandAlias))
        {
            return false;
        }

        var normalized = commandAlias!.Trim().TrimStart('.');
        return UnsafeExternalCommandAliases.Contains(normalized);
    }

    public static bool IsSupportedSkin(string? skin) =>
        string.Equals(skin, DefaultSkin, StringComparison.Ordinal) ||
        string.Equals(skin, CadDarkSkin, StringComparison.Ordinal) ||
        string.Equals(skin, PurpleGraySkin, StringComparison.Ordinal);

    public void Validate()
    {
        if (!Compatibility.IsFinite(Radius) || Radius < 120 || Radius > 320)
        {
            throw new InvalidOperationException("圆盘大小必须在 120 到 320 之间。");
        }

        if (!Compatibility.IsFinite(Opacity) || Opacity < 0.35 || Opacity > 1)
        {
            throw new InvalidOperationException("圆盘透明度必须在 0.35 到 1.0 之间。");
        }

        if (!IsSupportedSkin(Skin))
        {
            throw new InvalidOperationException("请选择有效的圆盘外观皮肤。");
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

        if (Items.Any(item => string.IsNullOrWhiteSpace(item.Name) != string.IsNullOrWhiteSpace(item.CommandAlias)))
        {
            throw new InvalidOperationException("圆盘位置可以整项留空；填写时命令名称和 CAD 快捷键必须同时填写。");
        }
    }

    private static RadialMenuItem Empty(int ring, int position) => Item(ring, position, string.Empty, string.Empty);

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
    private static readonly Dictionary<(int Ring, int Position, string Alias), string> LegacyDefaultAliases = new()
    {
        [(2, 9, "TCWJ")] = "DCTCWJ",
        [(2, 10, "TCMB")] = "DCTCMB",
        [(2, 11, "TCBZ")] = "DCTCBZ",
        [(2, 12, "TCXS")] = "DCTCXS",
        [(2, 13, "TXF")] = "DCTXF",
        [(2, 14, "DY")] = "DCDY",
        [(2, 1, "DCZ2")] = "DC22",
        [(2, 2, "DCZ3")] = "DC33"
    };

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
            ClearLegacyUnsupportedDefaults(options);
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
        Compatibility.NotNullOrWhiteSpace(path, nameof(path));
        Compatibility.NotNull(options, nameof(options));
        ClearLegacyUnsupportedDefaults(options);
        options.Validate();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(options, JsonOptions));
        Compatibility.MoveFileReplacingExisting(temporaryPath, path);
    }

    private static void ClearLegacyUnsupportedDefaults(RadialMenuOptions options)
    {
        if (!RadialMenuOptions.IsSupportedSkin(options.Skin))
        {
            options.Skin = RadialMenuOptions.DefaultSkin;
        }

        var obsolete = new HashSet<(string Name, string Alias)>
        {
            ("快速拉伸", "KS"), ("超级拉伸", "SS"),
            ("吊柜", "WG"), ("框架柜", "KJ"), ("切角柜", "JG"),
            ("轮廓矩形", "LK"), ("线转多段", "XD"), ("线去重", "XQC"),
            ("多线连接", "XLJ"), ("线打断", "XDD"), ("交点切断", "AB")
        };
        foreach (var item in options.Items ?? [])
        {
            if (RadialMenuOptions.IsUnsafeExternalCommandAlias(item.CommandAlias) ||
                obsolete.Contains((item.Name, item.CommandAlias)))
            {
                item.Name = string.Empty;
                item.CommandAlias = string.Empty;
            }

            if (LegacyDefaultAliases.TryGetValue(
                    (item.Ring, item.Position, item.CommandAlias),
                    out var migratedAlias))
            {
                item.CommandAlias = migratedAlias;
            }
        }
    }
}

public readonly record struct RadialMenuSlot(int Ring, int Position);

public static class RadialMenuHitTester
{
    public static RadialMenuSlot? HitTest(double x, double y, double radius, bool showThirdRing)
    {
        if (!Compatibility.IsFinite(radius) || radius <= 0)
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
