namespace WawiCallPro.ConsoleApp.Presentation;

internal sealed class SystemConsoleTerminal : IConsoleTerminal
{
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public TerminalSize GetSize() => new(Console.WindowWidth, Console.WindowHeight);
    public void Clear() => Console.Clear();
    public void WriteLine(string text) => Console.WriteLine(text);

    public async ValueTask<string?> ReadCommandAsync(int maximumLength, CancellationToken cancellationToken)
    {
        Console.Write(">");
        var input = new System.Text.StringBuilder();
        var size = GetSize();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GetSize() != size) return IConsoleTerminal.ResizeCommand;
            if (!Console.KeyAvailable)
            {
                await Task.Delay(40, cancellationToken);
                continue;
            }
            var key = Console.ReadKey(intercept: true);
            if (maximumLength == 0 && key.Key == ConsoleKey.Q) return "Q";
            if (key.Key == ConsoleKey.Enter) return input.ToString();
            if (key.Key == ConsoleKey.Backspace && input.Length > 0)
            {
                input.Length--;
                Console.Write("\b \b");
            }
            else if (char.IsAsciiLetterOrDigit(key.KeyChar) && input.Length < maximumLength)
            {
                input.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
    }
}
