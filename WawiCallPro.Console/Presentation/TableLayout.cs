using System.Globalization;
using System.Text;

namespace WawiCallPro.ConsoleApp.Presentation;

internal sealed record TableLine(string Text, int Row);

/// <summary>Uses terminal cells, including combining marks, common wide characters and emoji graphemes.</summary>
internal sealed class TableLayout
{
    private TableLayout(IReadOnlyList<string> header, IReadOnlyList<TableLine> lines)
    {
        Header = header;
        Lines = lines;
    }

    internal IReadOnlyList<string> Header { get; }
    internal IReadOnlyList<TableLine> Lines { get; }

    internal static TableLayout Create(ConsoleTable table, int width)
    {
        width = Math.Max(1, width);
        var header = Wrap(table.Title, width).ToList();
        header.AddRange(RenderRow(new ConsoleRow(table.NameHeader, table.ValueHeader), width));
        header.Add(new string('-', width));
        var lines = new List<TableLine>();
        for (var row = 0; row < table.Rows.Count; row++)
        {
            foreach (var line in RenderRow(table.Rows[row], width)) lines.Add(new TableLine(line, row));
            if (row < table.Rows.Count - 1) lines.Add(new TableLine("", row));
        }
        if (lines.Count == 0)
            lines.AddRange(Wrap("(Keine Einträge.)", width).Select(text => new TableLine(text, 0)));
        return new TableLayout(header, lines);
    }

    private static IEnumerable<string> RenderRow(ConsoleRow row, int width)
    {
        if (width < 24)
        {
            foreach (var line in Wrap(row.Name + ":", width)) yield return line;
            foreach (var line in Wrap(row.Value, width)) yield return line;
            yield break;
        }
        var nameWidth = Math.Clamp(width / 3, 8, 28);
        var values = Wrap(row.Value, width - nameWidth - 3);
        var names = Wrap(row.Name, nameWidth);
        for (var line = 0; line < Math.Max(names.Count, values.Count); line++)
        {
            var name = line < names.Count ? names[line] : "";
            var value = line < values.Count ? values[line] : "";
            yield return name + new string(' ', nameWidth - Measure(name)) + " | " + value;
        }
    }

    internal static IReadOnlyList<string> Wrap(string text, int width)
    {
        width = Math.Max(1, width);
        var result = new List<string>();
        var line = new StringBuilder();
        var cells = 0;
        // Control characters are shown literally, never interpreted as terminal escape sequences.
        var safe = new StringBuilder();
        foreach (var character in text.Replace("\r\n", "\n", StringComparison.Ordinal))
        {
            if (character == '\n') safe.Append(character);
            else if (character == '\t') safe.Append("    ");
            else if (char.IsControl(character)) safe.Append($"\\u{(int)character:X4}");
            else safe.Append(character);
        }
        var elements = StringInfo.GetTextElementEnumerator(safe.ToString());
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (element == "\n")
            {
                result.Add(line.ToString());
                line.Clear();
                cells = 0;
                continue;
            }
            var size = MeasureElement(element);
            if (cells + size > width && line.Length > 0)
            {
                result.Add(line.ToString());
                line.Clear();
                cells = 0;
            }
            // A two-cell grapheme cannot be displayed in a one-cell terminal. Use its visible escape form.
            if (size > width)
            {
                result.AddRange(element.EnumerateRunes().SelectMany(rune => Wrap($"\\U{rune.Value:X8}", width)));
                continue;
            }
            line.Append(element);
            cells += size;
        }
        result.Add(line.ToString());
        return result;
    }

    internal static int Measure(string text)
    {
        var cells = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext()) cells += MeasureElement(elements.GetTextElement());
        return cells;
    }

    private static int MeasureElement(string element)
    {
        var width = 0;
        foreach (var rune in element.EnumerateRunes())
        {
            if (rune.Value == 0xFE0F) { width = 2; continue; }
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format) continue;
            var value = rune.Value;
            var wide = value is >= 0x1100 and <= 0x115F or >= 0x2329 and <= 0x232A
                or >= 0x2E80 and <= 0xA4CF or >= 0xAC00 and <= 0xD7A3 or >= 0xF900 and <= 0xFAFF
                or >= 0xFE10 and <= 0xFE19 or >= 0xFE30 and <= 0xFE6F or >= 0xFF01 and <= 0xFF60
                or >= 0xFFE0 and <= 0xFFE6 or >= 0x1F000 and <= 0x1FAFF or >= 0x20000 and <= 0x3FFFD;
            width = Math.Max(width, wide ? 2 : 1);
        }
        return width;
    }
}
