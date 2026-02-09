namespace ServiceManager.Helpers;

public static class ConsoleHelper
{
    public static void WriteHighlight(string message)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(message);
        Console.ResetColor();
    }

    public static void WriteLineHighlight(string message)
    {
        Console.ForegroundColor = ConsoleColor.White;
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
}