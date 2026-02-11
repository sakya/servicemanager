namespace ServiceManager.Models;

public class SshTunnelConfig
{
    public string Name { get; set; } = null!;
    public string Host { get; set; } = null!;
    public uint LocalPort { get; set; }
    public uint RemotePort { get; set; }
    public string UserName { get; set; } = null!;
    public string Password { get; set; } = null!;
}