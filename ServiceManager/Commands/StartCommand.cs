using ServiceManager.Helpers;

namespace ServiceManager.Commands;

public class StartCommand : CommandBase
{
    public StartCommand(ServiceHelper serviceHelper) : base(serviceHelper)
    {
        Names = ["start"];
        ArgumentsSyntax = "NAME";
        Description = "Start a service";
    }

    public override async Task<bool> Run(string args)
    {
        if (string.IsNullOrEmpty(args)) {
            ConsoleHelper.WriteLineError("No service name provided");
            return false;
        }

        var service = ServiceHelper.GetService(args);
        if (service == null) {
            ConsoleHelper.WriteLineError($"Service '{args}' not found");
            return false;
        }
        await ServiceHelper.Start(service);
        return true;
    }
}