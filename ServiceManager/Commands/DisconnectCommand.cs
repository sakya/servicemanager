using ServiceManager.Helpers;
using ServiceManager.Models;

namespace ServiceManager.Commands;

public class DisconnectCommand : CommandBase
{
    private readonly SshTunnelConfig[] _configs;
    private readonly Dictionary<string, SshTunnel?> _sshTunnels;

    public DisconnectCommand(ServiceHelper serviceHelper, SshTunnelConfig[]? configs, Dictionary<string, SshTunnel?> sshTunnels) : base(serviceHelper)
    {
        Names = ["disconnect"];
        ArgumentsSyntax = "NAME";
        Description = "Disconnect a SSH tunnel";

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
            Console.Write($"Disconnecting SSH tunnel {args}...");
            if (sshTunnel != null) {
                if (sshTunnel.Status != SshTunnel.SshStatus.Connected) {
                    ConsoleHelper.WriteLineWarning("DISCONNECTED");
                } else {
                    if (await sshTunnel.Disconnect()) {
                        ConsoleHelper.WriteLineSuccess("DISCONNECTED");
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
                if (await t.Disconnect()) {
                    ConsoleHelper.WriteLineSuccess("DISCONNECTED");
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