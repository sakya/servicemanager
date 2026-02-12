using Renci.SshNet;
using Serilog.Core;

namespace ServiceManager.Helpers;

public class SshTunnel : IDisposable
{
    public enum SshStatus
    {
        Unknown = -1,
        Connecting,
        Connected,
        Disconnected
    }

    private SshClient? _sshClient;
    private ForwardedPortLocal? _forwardedPort;
    private readonly int _reconnectDelay;
    private Task? _reconnectTask;
    private CancellationTokenSource? _reconnectCts;
    private readonly Logger? _logger;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private volatile bool _disposed;

    public string Host { get; init; }
    public uint LocalPort { get; init; }
    public uint RemotePort { get; init; }
    public string Username { get; init; }
    public string Password { get; init; }
    public SshStatus Status { get; private set; }

    public SshTunnel(
        uint localPort,
        uint remotePort,
        string host,
        string username,
        string password,
        Logger? logger = null,
        int reconnectDelay = 1)
    {
        Host = host;
        LocalPort = localPort;
        RemotePort = remotePort;
        Username = username;
        Password = password;
        _logger = logger;
        _reconnectDelay = reconnectDelay;
    }

    public async Task<bool> Connect()
    {
        try {
            Status = SshStatus.Connecting;
            _sshClient = new SshClient(Host, Username, Password);
            _sshClient.ErrorOccurred += (_, eventArgs) =>
            {
                _logger?.Warning("SSH client error: {ExceptionMessage}", eventArgs.Exception.Message);
                Status = SshStatus.Disconnected;
            };
            await _sshClient.ConnectAsync(CancellationToken.None);
            if (_sshClient.IsConnected) {
                _forwardedPort = new ForwardedPortLocal("127.0.0.1", LocalPort, "127.0.0.1", RemotePort);
                _forwardedPort.Closing += (_, _) =>
                {
                    _logger?.Warning("Closing SSH tunnel");
                    Status = SshStatus.Disconnected;
                };
                _sshClient.AddForwardedPort(_forwardedPort);
                _forwardedPort.Start();
                if (_forwardedPort.IsStarted) {
                    Status = SshStatus.Connected;
                    _logger?.Information("SSH tunnel established: {LocalPort} -> {RemotePort}", LocalPort, RemotePort);
                    if (_reconnectTask == null) {
                        _reconnectCts = new CancellationTokenSource();
                        _reconnectTask = CheckConnectionTask(_reconnectCts.Token);
                    }

                    return true;
                }

                Status = SshStatus.Disconnected;
                _logger?.Warning("Failed to add forwarded port");
            }

        } catch (Exception ex) {
            _logger?.Error("Error connecting SSH tunnel: {ExMessage}", ex.Message);

            if (_forwardedPort != null) {
                _forwardedPort.Dispose();
                _forwardedPort = null;
            }

            if (_sshClient != null) {
                _sshClient.Dispose();
                _sshClient = null;
            }
        }

        return false;
    }

    public Task<bool> Disconnect()
    {
        if (_reconnectCts != null) {
            _reconnectCts.Cancel();
            _reconnectCts.Dispose();
        }

        if (_sshClient != null) {
            _logger?.Information("Disconnecting SSH client");
            if (_forwardedPort != null) {
                _sshClient.RemoveForwardedPort(_forwardedPort);
                _forwardedPort.Stop();
                _forwardedPort.Dispose();
                _forwardedPort = null;
            }
            _sshClient.Disconnect();
            _sshClient.Dispose();
            _sshClient = null;
        }

        if (_reconnectTask != null) {
            _reconnectTask.Dispose();
            _reconnectTask = null;
        }

        return Task.FromResult(true);
    }

    public void Dispose()
    {
        _disposed = true;
        Disconnect();
    }

    private async Task Reconnect(CancellationToken ct)
    {
        if (_disposed || ct.IsCancellationRequested)
            return;

        await _reconnectLock.WaitAsync(ct);
        try {
            if (_reconnectDelay > 0) {
                await Task.Delay(_reconnectDelay * 1000, ct);
            }

            _logger?.Information("Reconnecting SSH client");
            if (_sshClient != null) {
                if (_forwardedPort != null) {
                    _sshClient.RemoveForwardedPort(_forwardedPort);
                    _forwardedPort.Stop();
                    _forwardedPort.Dispose();
                }

                _sshClient.Disconnect();
                _sshClient.Dispose();
            }

            if (!await Connect()) {
                _logger?.Warning("Failed to reconnect SSH client");
            }
        } catch (OperationCanceledException) {
            // Ignored
        } finally {
            _reconnectLock.Release();
        }
    }

    private async Task CheckConnectionTask(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested) {
            try {
                if (_sshClient != null) {
                    var cmd = _sshClient.CreateCommand("true");
                    cmd.CommandTimeout = TimeSpan.FromSeconds(5);
                    cmd.Execute();
                }
            } catch {
                _logger?.Warning("SSH client disconnected");
                Status = SshStatus.Disconnected;
                await Reconnect(ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}