using ServiceManager.Helpers;

namespace ServiceManager.Commands;

public class StatusCommand : CommandBase
{
    private Dictionary<string, SshTunnel?> _sshTunnels;
    public StatusCommand(ServiceHelper serviceHelper, Dictionary<string, SshTunnel?> sshTunnels) : base(serviceHelper)
    {
        _sshTunnels = sshTunnels;
        Names = ["status", "list"];
        Description = "List all services/SSH tunnels status";
    }

    public override Task<bool> Run(string args)
    {
        if (!string.IsNullOrEmpty(args)) {
            ConsoleHelper.WriteLineError("Command 'status' doesn't have arguments");
            return Task.FromResult(false);
        }

        var i = 0;
        if (_sshTunnels.Count > 0) {
            ConsoleHelper.WriteLineHighlight("SSH tunnels status:");
            foreach (var kvp in _sshTunnels) {
                Console.Write($"[{++i,2}] {kvp.Key}");
                Console.CursorLeft = 40;
                if (kvp.Value?.Status == SshTunnel.SshStatus.Connected) {
                    ConsoleHelper.WriteLineSuccess("Connected");
                } else {
                    ConsoleHelper.WriteLineError("Disconnected");
                }
            }
            Console.WriteLine();
        }

        ConsoleHelper.WriteLineHighlight("Service status:");
        i = 0;
        foreach (var service in ServiceHelper.Services) {
            Console.Write($"[{++i,2}] {service.Name}");
            Console.CursorLeft = 40;
            if (ServiceHelper.IsRunning(service)) {
                ConsoleHelper.WriteSuccess("Running");
            } else {
                ConsoleHelper.WriteError("Stopped");
            }
            Console.CursorLeft = 50;
            Console.WriteLine(service.Url);
        }
        return Task.FromResult(true);
    }
}