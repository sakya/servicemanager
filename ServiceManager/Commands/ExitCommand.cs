using ServiceManager.Helpers;

namespace ServiceManager.Commands;

public class ExitCommand : CommandBase
{
    private readonly CancellationTokenSource _cts;

    public ExitCommand(ServiceHelper serviceHelper, CancellationTokenSource cts) : base(serviceHelper)
    {
        Names = ["exit", "quit"];
        Description = "Stop all services and exit";

        _cts = cts;
    }

    public override async Task<bool> Run(string args)
    {
        if (!string.IsNullOrEmpty(args)) {
            ConsoleHelper.WriteLineError("Command 'quit' doesn't have arguments");
            return false;
        }

        Console.WriteLine("Stopping services:");
        foreach (var service in ServiceHelper.Services) {
            Console.Write("  ");
            await ServiceHelper.Stop(service);
        }

        await _cts.CancelAsync();
        return true;
    }
}