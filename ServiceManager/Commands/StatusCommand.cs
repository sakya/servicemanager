using ServiceManager.Helpers;

namespace ServiceManager.Commands;

public class StatusCommand : CommandBase
{
    public StatusCommand(ServiceHelper serviceHelper) : base(serviceHelper)
    {
        Names = ["status", "list"];
        Description = "List all services status";
    }

    public override Task<bool> Run(string args)
    {
        if (!string.IsNullOrEmpty(args)) {
            ConsoleHelper.WriteLineError("Command 'status' doesn't have arguments");
            return Task.FromResult(false);
        }

        ConsoleHelper.WriteLineHighlight("Service status:");
        var i = 0;
        foreach (var service in ServiceHelper.Services) {
            Console.Write($"[{++i}] {service.Name}");
            Console.CursorLeft = 40;
            if (ServiceHelper.IsRunning(service)) {
                ConsoleHelper.WriteLineSuccess("Running");
            } else {
                ConsoleHelper.WriteLineError("Stopped");
            }
        }
        return Task.FromResult(true);
    }
}