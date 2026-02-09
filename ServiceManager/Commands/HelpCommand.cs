using ServiceManager.Helpers;

namespace ServiceManager.Commands;

public class HelpCommand : CommandBase
{
    private readonly List<CommandBase> _commands;

    public HelpCommand(ServiceHelper serviceHelper, List<CommandBase> commands) : base(serviceHelper)
    {
        Names = ["help"];
        Description = "Display help";
        _commands = commands;
    }

    public override Task<bool> Run(string args)
    {
        if (!string.IsNullOrEmpty(args)) {
            ConsoleHelper.WriteLineError("Command 'help' doesn't have arguments");
            return Task.FromResult(false);
        }

        Console.WriteLine("Available commands:");
        Console.WriteLine();
        ConsoleHelper.WriteLineHighlight($"{"Command", -30}Description");
        foreach (var command in _commands.OrderBy(c => c.Names.FirstOrDefault())) {
            Console.WriteLine($"{$"{string.Join("|", command.Names)} {command.ArgumentsSyntax}".Trim(), -30}{command.Description}");
        }

        return Task.FromResult(true);
    }
}