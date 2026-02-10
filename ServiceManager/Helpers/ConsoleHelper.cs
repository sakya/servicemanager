using System.Text;

namespace ServiceManager.Helpers;

public static class ConsoleHelper
{
    public static bool InputDisabled = false;

    public static void WriteHighlight(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkBlue;
        Console.Write(message);
        Console.ResetColor();
    }

    public static void WriteLineHighlight(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkBlue;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write(message);
        Console.ResetColor();
    }

    public static void WriteLineSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteInfo(string message)
    {
        Console.ResetColor();
        Console.Write(message);
    }

    public static void WriteLineInfo(string message)
    {
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write(message);
        Console.ResetColor();
    }

    public static void WriteLineWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.Write(message);
        Console.ResetColor();
    }

    public static void WriteLineError(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static string ReadLine(CancellationToken ct = default, int startLeft = 0)
    {
        var buffer = new StringBuilder();
        var cursor = 0;

        while (!ct.IsCancellationRequested) {
            var key = Console.ReadKey(true);
            if (InputDisabled) {
                continue;
            }

            switch (key.Key) {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return buffer.ToString();

                case ConsoleKey.LeftArrow:
                    if (cursor > 0) {
                        cursor--;
                        SetCursor(startLeft, Console.CursorTop, cursor);
                    }
                    break;
                case ConsoleKey.RightArrow:
                    if (cursor < buffer.Length) {
                        cursor++;
                        SetCursor(startLeft, Console.CursorTop, cursor);
                    }
                    break;

                case ConsoleKey.Home:
                    cursor = 0;
                    SetCursor(startLeft, Console.CursorTop, cursor);
                    break;

                case ConsoleKey.End:
                    cursor = buffer.Length;
                    SetCursor(startLeft, Console.CursorTop, cursor);
                    break;

                case ConsoleKey.Backspace:
                    if (cursor > 0) {
                        buffer.Remove(cursor - 1, 1);
                        cursor--;
                        RedrawLine(startLeft, Console.CursorTop, buffer, cursor);
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursor < buffer.Length) {
                        buffer.Remove(cursor, 1);
                        RedrawLine(startLeft, Console.CursorTop, buffer, cursor);
                    }
                    break;

                default:
                    if (!char.IsControl(key.KeyChar)) {
                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                        RedrawLine(startLeft, Console.CursorTop, buffer, cursor);
                    }
                    break;
            }
        }

        return string.Empty;
    }

    private static void RedrawLine(int startLeft, int startTop, StringBuilder buffer, int cursor)
    {
        Console.SetCursorPosition(startLeft, startTop);
        Console.Write(buffer.ToString());
        Console.Write(' ');
        Console.SetCursorPosition(startLeft + cursor, startTop);
    }

    private static void SetCursor(int startLeft, int startTop, int cursor)
    {
        Console.SetCursorPosition(startLeft + cursor, startTop);
    }
}