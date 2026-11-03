namespace WawiCallPro.ConsoleApp.Presentation;

internal sealed record ConsoleRow(string Name, string Value);

internal sealed record ConsoleTable(string Title, string NameHeader, string ValueHeader, IReadOnlyList<ConsoleRow> Rows);

internal readonly record struct TerminalSize(int Width, int Height);

internal interface IConsoleTerminal
{
    const string ResizeCommand = "\0resize";
    bool IsInteractive { get; }
    TerminalSize GetSize();
    void Clear();
    void WriteLine(string text);
    ValueTask<string?> ReadCommandAsync(int maximumLength, CancellationToken cancellationToken);
}
