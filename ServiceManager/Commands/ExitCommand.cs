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
            ConsoleHelper.WriteLineError("Command 'exit' doesn't have arguments");
            return false;
        }

        ConsoleHelper.WriteLineHighlight("Stopping services:");
        foreach (var service in ServiceHelper.Services) {
            try {
                await ServiceHelper.Stop(service);
            } catch (Exception ex) {
                Console.WriteLine();
                ConsoleHelper.WriteLineError($"Failed to stop service {service.Name}: {ex.Message}");
                Program.Logger?.Error("Error stopping service {serviceName}: {ExMessage}", service.Name, ex.Message);
            }
        }

        await _cts.CancelAsync();
        return true;
    }
}