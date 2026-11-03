using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.ConsoleApp.Presentation;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class TablePagerTests
{
    [TestMethod]
    [DataRow(80, 24)]
    [DataRow(32, 8)]
    [DataRow(15, 6)]
    public async Task PagesFitTheTerminalAndVisitEveryRowIncludingRowsTallerThanTheViewport(int width, int height)
    {
        var rows = Enumerable.Range(0, 30).Select(index => new ConsoleRow($"KEY-{index:000}", $"VALUE-{index:000}")).ToList();
        rows.Insert(5, new ConsoleRow("TALL-ROW", string.Join("\n", Enumerable.Range(0, 40).Select(index => $"part{index:000}"))));
        var terminal = new FakeTerminal(new TerminalSize(width, height));
        terminal.Input = () => ValueTask.FromResult<string?>(terminal.PageNumber() < terminal.PageCount() ? "N" : "Q");
        await new TablePager(terminal).WriteAsync(new ConsoleTable("Diagnose", "Name", "Wert", rows), default);
        AssertFits(terminal);
        var all = string.Join("\n", terminal.Frames.SelectMany(frame => frame.Lines));
        foreach (var index in Enumerable.Range(0, 30))
        {
            Assert.AreEqual(1, Regex.Matches(all, $"KEY-{index:000}").Count);
            Assert.AreEqual(1, Regex.Matches(all, $"VALUE-{index:000}").Count);
        }
        foreach (var index in Enumerable.Range(0, 40)) Assert.AreEqual(1, Regex.Matches(all, $"part{index:000}").Count);
        Assert.IsTrue(terminal.Frames.Count > 1);
    }

    [TestMethod]
    public async Task NavigationSupportsPreviousNextGoToAndInvalidTargetsWithoutLosingThePage()
    {
        var input = new Queue<string>(["P", "N", "P", "G", "3", "G", "999", "G", "bad", "Q"]);
        var terminal = new FakeTerminal(new TerminalSize(60, 12)) { Input = () => ValueTask.FromResult<string?>(input.Dequeue()) };
        await new TablePager(terminal).WriteAsync(ManyRows(), default);
        var pages = terminal.Frames.Select(frame => FakeTerminal.Page(frame.Lines)).Where(page => page is not null).Select(page => page!.Value.Number).ToArray();
        CollectionAssert.AreEqual(new[] { 1, 1, 2, 1, 3, 3, 3 }, pages);
        Assert.HasCount(0, input);
        Assert.IsTrue(terminal.Frames.Any(frame => frame.Lines.Any(line => line.Contains("? N/P/G/Q", StringComparison.Ordinal))));
        AssertFits(terminal);
    }

    [TestMethod]
    public async Task ResizeReflowsTheTableAndKeepsTheCurrentRowInView()
    {
        var terminal = new FakeTerminal(new TerminalSize(80, 20));
        var input = 0;
        terminal.Input = () =>
        {
            input++;
            if (input == 1) return ValueTask.FromResult<string?>("N");
            if (input == 2)
            {
                terminal.Size = new TerminalSize(32, 9);
                return ValueTask.FromResult<string?>(IConsoleTerminal.ResizeCommand);
            }
            return ValueTask.FromResult<string?>("Q");
        };
        await new TablePager(terminal).WriteAsync(ManyRows(), default);
        Assert.HasCount(3, terminal.Frames);
        var previousKeys = Regex.Matches(string.Join("\n", terminal.Frames[1].Lines), "KEY-[0-9]{3}").Select(match => match.Value).ToArray();
        var resized = string.Join("\n", terminal.Frames[2].Lines);
        Assert.IsTrue(previousKeys.Any(key => resized.Contains(key, StringComparison.Ordinal)));
        AssertFits(terminal);
    }

    [TestMethod]
    public async Task RedirectedOutputContainsEveryCellAndNeverQueriesOrReadsTheTerminal()
    {
        var terminal = new FakeTerminal(default) { IsInteractive = false };
        await new TablePager(terminal).WriteAsync(ManyRows(), default);
        var output = string.Join("\n", terminal.RedirectedLines);
        foreach (var index in Enumerable.Range(0, 40))
        {
            Assert.Contains($"KEY-{index:000}", output);
            Assert.Contains($"VALUE-{index:000}", output);
        }
        Assert.DoesNotContain("N/P/G/Q", output);
        Assert.HasCount(0, terminal.Frames);
    }

    [TestMethod]
    public async Task CancellationWhileWaitingForInputIsObserved()
    {
        using var cancellation = new CancellationTokenSource();
        var terminal = new FakeTerminal(new TerminalSize(60, 12));
        terminal.Input = async () =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
            return null;
        };
        await Assert.ThrowsAsync<OperationCanceledException>(() => new TablePager(terminal).WriteAsync(ManyRows(), cancellation.Token));
    }

    [TestMethod]
    public async Task EmptyTableAndVerySmallTerminalCanBeClosed()
    {
        var terminal = new FakeTerminal(new TerminalSize(10, 4));
        await new TablePager(terminal).WriteAsync(new ConsoleTable("Leer", "Name", "Wert", []), default);
        AssertFits(terminal);
        terminal = new FakeTerminal(new TerminalSize(50, 12));
        await new TablePager(terminal).WriteAsync(new ConsoleTable("Leer", "Name", "Wert", []), default);
        Assert.Contains("(Keine Einträge.)", string.Join("\n", terminal.Frames[0].Lines));
    }

    [TestMethod]
    public void WrappingPreservesGraphemesAndNeutralizesTerminalControlSequences()
    {
        const string text = "Größe e\u0301 中 🔐 👩‍💻";
        var wrapped = TableLayout.Wrap(text, 8);
        Assert.AreEqual(text, string.Concat(wrapped));
        Assert.IsTrue(wrapped.All(line => TableLayout.Measure(line) <= 8));
        Assert.IsTrue(wrapped.Any(line => line.Contains("👩‍💻", StringComparison.Ordinal)));
        var controls = string.Join("", TableLayout.Wrap("name\u001b[2J\rvalue\tend", 9));
        Assert.DoesNotContain("\u001b", controls);
        Assert.Contains("\\u001B[2J\\u000Dvalue    end", controls);
    }

    private static ConsoleTable ManyRows() => new("Status", "Name", "Wert", Enumerable.Range(0, 40)
        .Select(index => new ConsoleRow($"KEY-{index:000}", $"VALUE-{index:000}")).ToArray());

    private static void AssertFits(FakeTerminal terminal)
    {
        foreach (var frame in terminal.Frames)
        {
            Assert.IsTrue(frame.Lines.Count + 1 <= frame.Size.Height, "Page and input prompt must fit vertically.");
            Assert.IsTrue(frame.Lines.All(line => TableLayout.Measure(line) < frame.Size.Width), "Wrapped cells must fit horizontally.");
        }
    }

    private sealed record Frame(TerminalSize Size, List<string> Lines);

    private sealed class FakeTerminal(TerminalSize size) : IConsoleTerminal
    {
        public bool IsInteractive { get; init; } = true;
        internal TerminalSize Size { get; set; } = size;
        internal List<Frame> Frames { get; } = [];
        internal List<string> RedirectedLines { get; } = [];
        internal Func<ValueTask<string?>> Input { get; set; } = () => ValueTask.FromResult<string?>("Q");

        public TerminalSize GetSize()
        {
            Assert.IsTrue(IsInteractive, "Redirected output must not query a viewport.");
            return Size;
        }

        public void Clear()
        {
            Assert.IsTrue(IsInteractive);
            Frames.Add(new Frame(Size, []));
        }

        public void WriteLine(string text)
        {
            if (IsInteractive) Frames[^1].Lines.Add(text);
            else RedirectedLines.Add(text);
        }

        public ValueTask<string?> ReadCommandAsync(int maximumLength, CancellationToken cancellationToken)
        {
            Assert.IsTrue(IsInteractive, "Redirected output must never wait for input.");
            return Input();
        }

        internal int PageNumber() => Page(Frames[^1].Lines)!.Value.Number;
        internal int PageCount() => Page(Frames[^1].Lines)!.Value.Count;
        internal static (int Number, int Count)? Page(List<string> lines)
        {
            for (var index = Math.Max(0, lines.Count - 3); index < lines.Count; index++)
            {
                var match = Regex.Match(string.Concat(lines.Skip(index)), @"^(\d+)/(\d+)(?: \?)? N/P/G/Q$");
                if (match.Success) return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            }
            return null;
        }
    }
}
