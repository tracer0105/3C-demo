using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Cim.DeviceSimulator.Simulation;

/// <summary>
/// Simple TCP text control port that lets external clients send commands to the simulator.
/// Default port: 7001
/// <para>Commands:</para>
/// <list type="bullet">
/// <item>STATE equipmentId newState   – force a state transition</item>
/// <item>ALARM equipmentId code level desc – raise an alarm</item>
/// <item>CLEAR equipmentId code        – clear an alarm</item>
/// <item>TEST  equipmentId              – emit a test result</item>
/// <item>STATUS                         – list all equipment states</item>
/// <item>QUIT                           – close this connection</item>
/// </list>
/// </summary>
public class TcpControlServer
{
    private readonly int _port;
    private readonly Dictionary<string, EquipmentSimulator> _simulators;
    private readonly ILogger<TcpControlServer> _logger;
    private TcpListener? _listener;

    public TcpControlServer(int port, Dictionary<string, EquipmentSimulator> simulators, ILogger<TcpControlServer> logger)
    {
        _port = port;
        _simulators = simulators;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _logger.LogInformation("TCP control port listening on 127.0.0.1:{Port}", _port);
        _logger.LogInformation("Commands: STATE <eqId> <state> | ALARM <eqId> <code> <level> <desc> | CLEAR <eqId> <code> | TEST <eqId> | STATUS | QUIT");

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "TCP accept error"); continue; }

            var clientTask = HandleClientAsync(client, ct);
            _ = clientTask.ContinueWith(
                t => _logger.LogError(t.Exception, "Unhandled exception in TCP client handler"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        _listener.Stop();
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var tcp = client;
        using var ns = tcp.GetStream();
        using var reader = new StreamReader(ns, Encoding.UTF8);
        using var writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

        await writer.WriteLineAsync("3C-CIM Simulator Control Port. Type HELP for commands.");

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var cmd = parts[0].ToUpperInvariant();
            string response;

            try
            {
                response = cmd switch
                {
                    "STATE" when parts.Length >= 3 => await CmdStateAsync(parts, ct),
                    "ALARM" when parts.Length >= 5 => await CmdAlarmAsync(parts, ct),
                    "CLEAR" when parts.Length >= 3 => await CmdClearAsync(parts, ct),
                    "TEST"  when parts.Length >= 2 => await CmdTestAsync(parts, ct),
                    "STATUS" => CmdStatus(),
                    "HELP" => "Commands: STATE <eqId> <state> | ALARM <eqId> <code> <level> <desc> | CLEAR <eqId> <code> | TEST <eqId> | STATUS | QUIT",
                    "QUIT" => "BYE",
                    _ => $"ERR Unknown command '{cmd}'. Type HELP."
                };
            }
            catch (Exception ex)
            {
                response = $"ERR {ex.Message}";
            }

            await writer.WriteLineAsync(response);
            if (cmd == "QUIT") break;
        }
    }

    private async Task<string> CmdStateAsync(string[] parts, CancellationToken ct)
    {
        var eqId = parts[1];
        var state = parts[2].ToUpperInvariant();
        if (!_simulators.TryGetValue(eqId, out var sim))
            return $"ERR Equipment '{eqId}' not found.";
        await sim.TransitionStateAsync(state, null, null, ct);
        return $"OK {eqId} → {state}";
    }

    private async Task<string> CmdAlarmAsync(string[] parts, CancellationToken ct)
    {
        var eqId = parts[1];
        var code = parts[2];
        var level = parts[3].ToUpperInvariant();
        var desc = string.Join(' ', parts[4..]);
        if (!_simulators.TryGetValue(eqId, out var sim))
            return $"ERR Equipment '{eqId}' not found.";
        await sim.RaiseAlarmAsync(code, level, desc, ct);
        return $"OK Alarm {code} raised on {eqId}";
    }

    private async Task<string> CmdClearAsync(string[] parts, CancellationToken ct)
    {
        var eqId = parts[1];
        var code = parts[2];
        if (!_simulators.TryGetValue(eqId, out var sim))
            return $"ERR Equipment '{eqId}' not found.";
        await sim.ClearAlarmAsync(code, ct);
        return $"OK Alarm {code} cleared on {eqId}";
    }

    private async Task<string> CmdTestAsync(string[] parts, CancellationToken ct)
    {
        var eqId = parts[1];
        if (!_simulators.TryGetValue(eqId, out var sim))
            return $"ERR Equipment '{eqId}' not found.";
        await sim.EmitTestResultAsync(ct);
        return $"OK Test result emitted for {eqId}";
    }

    private string CmdStatus()
    {
        var lines = _simulators.Select(kv => $"  {kv.Key}: {kv.Value.CurrentState}");
        return "Equipment states:\n" + string.Join("\n", lines);
    }
}
