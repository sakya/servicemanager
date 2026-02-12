using ServiceManager.Helpers;
using ServiceManager.Models;

namespace ServiceManager.Commands;

public class ConnectCommand: CommandBase
{
    private readonly SshTunnelConfig[] _configs;
    private readonly Dictionary<string, SshTunnel?> _sshTunnels;

    public ConnectCommand(ServiceHelper serviceHelper, SshTunnelConfig[]? configs, Dictionary<string, SshTunnel?> sshTunnels) : base(serviceHelper)
    {
        Names = ["connect"];
        ArgumentsSyntax = "NAME";
        Description = "Connect a SSH tunnel";

        _configs = configs ?? [];
        _sshTunnels = sshTunnels;
    }

    public override async Task<bool> Run(string args)
    {
        if (string.IsNullOrEmpty(args)) {
            ConsoleHelper.WriteLineError("No SSH tunnel name provided");
            return false;
        }

        if (_sshTunnels.TryGetValue(args, out var sshTunnel)) {
            Console.Write($"Connecting SSH tunnel {args}...");
            if (sshTunnel != null) {
                if (sshTunnel.Status == SshTunnel.SshStatus.Connected) {
                    ConsoleHelper.WriteLineWarning("CONNECTED");
                } else {
                    if (await sshTunnel.Connect()) {
                        ConsoleHelper.WriteLineSuccess("CONNECTED");
                    } else {
                        ConsoleHelper.WriteLineError("FAILED");
                    }
                }
            } else {
                var cfg = _configs.FirstOrDefault(x => string.Compare(x.Name, args, StringComparison.InvariantCultureIgnoreCase) == 0);
                if (cfg == null) {
                    ConsoleHelper.WriteLineError($"SSH tunnel '{args}' not found");
                    return false;
                }

                var t = new SshTunnel(
                    cfg.LocalPort, cfg.RemotePort,
                    cfg.Host,
                    cfg.UserName, cfg.Password,
                    Program.Logger);
                if (await t.Connect()) {
                    ConsoleHelper.WriteLineSuccess("CONNECTED");
                } else {
                    ConsoleHelper.WriteLineError("FAILED");
                }
                _sshTunnels[cfg.Name] = t;
            }
        } else {
            ConsoleHelper.WriteLineError($"SSH tunnel '{args}' not found");
            return false;
        }

        return true;
    }
}