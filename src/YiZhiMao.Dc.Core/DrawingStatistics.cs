using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace YiZhiMao.Dc.Core;

public sealed record DrawingPart(
    string Category,
    string Name,
    double Width,
    double Height,
    double Thickness,
    int Quantity = 1);

public sealed record DrawingStatisticsRow(
    string Category,
    string Name,
    double Width,
    double Height,
    double Thickness,
    int Quantity)
{
    public double AreaSquareMeters => Width * Height * Quantity / 1_000_000d;
}

public static class DrawingStatistics
{
    public static IReadOnlyList<DrawingStatisticsRow> Aggregate(IEnumerable<DrawingPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return parts
            .Where(part => part.Quantity > 0 && part.Width > 0 && part.Height > 0 && part.Thickness >= 0)
            .GroupBy(part => new
            {
                part.Category,
                part.Name,
                Width = Math.Round(part.Width, 2),
                Height = Math.Round(part.Height, 2),
                Thickness = Math.Round(part.Thickness, 2)
            })
            .Select(group => new DrawingStatisticsRow(
                group.Key.Category,
                group.Key.Name,
                group.Key.Width,
                group.Key.Height,
                group.Key.Thickness,
                group.Sum(part => part.Quantity)))
            .OrderBy(row => row.Category, StringComparer.CurrentCulture)
            .ThenBy(row => row.Name, StringComparer.CurrentCulture)
            .ThenBy(row => row.Width)
            .ThenBy(row => row.Height)
            .ToArray();
    }

    public static void ExportCsv(string path, IEnumerable<DrawingStatisticsRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder("类别,名称,宽度(mm),高度(mm),厚度(mm),数量,面积(m²)\r\n");
        foreach (var row in rows)
        {
            builder.Append(Escape(row.Category)).Append(',')
                .Append(Escape(row.Name)).Append(',')
                .Append(Format(row.Width)).Append(',')
                .Append(Format(row.Height)).Append(',')
                .Append(Format(row.Thickness)).Append(',')
                .Append(row.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.AreaSquareMeters.ToString("0.####", CultureInfo.InvariantCulture))
                .Append("\r\n");
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static void ExportExcelXml(string path, string worksheetName, IEnumerable<DrawingStatisticsRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        ArgumentNullException.ThrowIfNull(rows);

        XNamespace spreadsheet = "urn:schemas-microsoft-com:office:spreadsheet";
        XNamespace office = "urn:schemas-microsoft-com:office:office";
        XNamespace excel = "urn:schemas-microsoft-com:office:excel";
        var table = new XElement(spreadsheet + "Table");
        table.Add(CreateExcelRow(spreadsheet,
            ("类别", "String"), ("名称", "String"), ("宽度(mm)", "String"),
            ("高度(mm)", "String"), ("厚度(mm)", "String"), ("数量", "String"),
            ("面积(m²)", "String")));
        foreach (var row in rows)
        {
            table.Add(CreateExcelRow(spreadsheet,
                (row.Category, "String"),
                (row.Name, "String"),
                (Format(row.Width), "Number"),
                (Format(row.Height), "Number"),
                (Format(row.Thickness), "Number"),
                (row.Quantity.ToString(CultureInfo.InvariantCulture), "Number"),
                (row.AreaSquareMeters.ToString("0.####", CultureInfo.InvariantCulture), "Number")));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XProcessingInstruction("mso-application", "progid=\"Excel.Sheet\""),
            new XElement(spreadsheet + "Workbook",
                new XAttribute(XNamespace.Xmlns + "ss", spreadsheet),
                new XAttribute(XNamespace.Xmlns + "o", office),
                new XAttribute(XNamespace.Xmlns + "x", excel),
                new XElement(spreadsheet + "Worksheet",
                    new XAttribute(spreadsheet + "Name", worksheetName),
                    table)));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        document.Save(path);
    }

    private static XElement CreateExcelRow(
        XNamespace spreadsheet,
        params (string Value, string Type)[] cells) =>
        new(spreadsheet + "Row",
            cells.Select(cell => new XElement(spreadsheet + "Cell",
                new XElement(spreadsheet + "Data",
                    new XAttribute(spreadsheet + "Type", cell.Type),
                    cell.Value))));

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
}
