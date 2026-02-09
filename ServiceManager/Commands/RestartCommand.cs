using ServiceManager.Helpers;

namespace ServiceManager.Commands;

public class RestartCommand : CommandBase
{
    public RestartCommand(ServiceHelper serviceHelper) : base(serviceHelper)
    {
        Names = ["restart"];
        ArgumentsSyntax = "NAME";
        Description = "Restart a service";
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
        await ServiceHelper.Stop(service);
        await ServiceHelper.Start(service);

        return true;
    }
}