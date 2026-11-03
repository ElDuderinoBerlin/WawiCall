using System.Globalization;

namespace WawiCallPro.ConsoleApp.Presentation;

internal sealed class TablePager(IConsoleTerminal terminal)
{
    internal async Task WriteAsync(ConsoleTable table, CancellationToken cancellationToken)
    {
        if (!terminal.IsInteractive)
        {
            // No viewport queries, clearing or input reads when either stream is redirected.
            var complete = TableLayout.Create(table, 120);
            foreach (var line in complete.Header) terminal.WriteLine(line);
            foreach (var line in complete.Lines) terminal.WriteLine(line.Text);
            return;
        }

        var page = 0;
        var anchorRow = 0;
        var previousSize = default(TerminalSize);
        var invalid = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var size = terminal.GetSize();
            var width = Math.Max(1, size.Width - 1); // Leave the final column unused to prevent terminal autowrap.
            terminal.Clear();
            if (size.Width < 12 || size.Height < 6)
            {
                foreach (var line in TableLayout.Wrap("Terminal vergrößern; Q beendet.", width).Take(Math.Max(0, size.Height - 2)))
                    terminal.WriteLine(line);
                var tinyCommand = await terminal.ReadCommandAsync(Math.Max(0, width - 1), cancellationToken);
                if (tinyCommand is null || tinyCommand.Equals("Q", StringComparison.OrdinalIgnoreCase)) return;
                continue;
            }

            var layout = TableLayout.Create(table, width);
            var footerHeight = TableLayout.Wrap($"{layout.Lines.Count}/{layout.Lines.Count} ? N/P/G/Q", width).Count;
            var header = layout.Header;
            if (header.Count + footerHeight + 3 > size.Height) header = [];
            var capacity = Math.Max(1, size.Height - header.Count - footerHeight - 2);
            var pageCount = (layout.Lines.Count + capacity - 1) / capacity;
            if (previousSize != default && previousSize != size)
            {
                var anchor = layout.Lines.Select((line, index) => (line, index)).FirstOrDefault(item => item.line.Row >= anchorRow);
                page = anchor.index / capacity;
            }
            page = Math.Clamp(page, 0, pageCount - 1);
            anchorRow = layout.Lines[page * capacity].Row;
            previousSize = size;
            foreach (var line in header) terminal.WriteLine(line);
            foreach (var line in layout.Lines.Skip(page * capacity).Take(capacity)) terminal.WriteLine(line.Text);
            foreach (var line in TableLayout.Wrap($"{page + 1}/{pageCount}{(invalid ? " ?" : "")} N/P/G/Q", width)) terminal.WriteLine(line);
            var command = await terminal.ReadCommandAsync(Math.Min(10, width - 1), cancellationToken);
            invalid = false;
            switch (command?.Trim().ToUpperInvariant())
            {
                case null or "Q": return;
                case "\0RESIZE": break;
                case "N": page = Math.Min(page + 1, pageCount - 1); break;
                case "P": page = Math.Max(page - 1, 0); break;
                case "G":
                    terminal.Clear();
                    foreach (var line in TableLayout.Wrap($"Seite (1..{pageCount}), Enter:", width)) terminal.WriteLine(line);
                    var target = await terminal.ReadCommandAsync(Math.Min(10, width - 1), cancellationToken);
                    if (target is null) return;
                    if (target == IConsoleTerminal.ResizeCommand) continue;
                    if (int.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= pageCount)
                        page = number - 1;
                    else invalid = true;
                    break;
                default: invalid = true; break;
            }
        }
    }
}
