using Cim.DbAdapter.EventBus;
using Cim.DbAdapter.Models;
using Microsoft.Extensions.Logging;

namespace Cim.DeviceSimulator.Simulation;

/// <summary>
/// Simulates a shop-floor equipment/PLC for 3C production testing.
/// Emits state changes, alarms and test results at configurable intervals.
/// </summary>
public class EquipmentSimulator
{
    private readonly string _equipmentId;
    private readonly string _equipmentName;
    private readonly IEventBus _bus;
    private readonly ILogger<EquipmentSimulator> _logger;
    private readonly Random _rng = new();

    private string _currentState = "IDLE";
    private string? _currentLot;
    private string? _currentRecipe;
    private int _snCounter = 1;

    // State machine: IDLE → RUNNING → IDLE (normal), RUNNING → ALARM → MAINTENANCE → IDLE
    private static readonly string[] States = { "IDLE", "RUNNING", "ALARM", "MAINTENANCE", "DOWN" };

    public string EquipmentId => _equipmentId;
    public string CurrentState => _currentState;

    public EquipmentSimulator(string equipmentId, string equipmentName, IEventBus bus, ILogger<EquipmentSimulator> logger)
    {
        _equipmentId = equipmentId;
        _equipmentName = equipmentName;
        _bus = bus;
        _logger = logger;
    }

    public async Task RunCycleAsync(CancellationToken ct)
    {
        // Normal production cycle
        if (_currentState == "IDLE")
        {
            await TransitionStateAsync("RUNNING", $"LOT-{DateTime.UtcNow:yyyyMMdd}-{_rng.Next(100, 999)}", "RCP-001", ct);

            // Simulate producing 1-3 SNs per cycle
            var count = _rng.Next(1, 4);
            for (int i = 0; i < count && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(_rng.Next(1, 3)), ct);
                await EmitTestResultAsync(ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);

            // Occasionally raise an alarm
            if (_rng.Next(0, 10) < 2)
            {
                await RaiseAlarmAsync("ALM-001", "WARNING", "Temperature above threshold", ct);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                await ClearAlarmAsync("ALM-001", ct);
            }

            await TransitionStateAsync("IDLE", null, null, ct);
        }
        else if (_currentState == "ALARM")
        {
            _logger.LogWarning("[{EqId}] Equipment in ALARM state – auto-recovering…", _equipmentId);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            await ClearAlarmAsync("ALM-001", ct);
            await TransitionStateAsync("MAINTENANCE", null, null, ct);
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            await TransitionStateAsync("IDLE", null, null, ct);
        }
    }

    public async Task TransitionStateAsync(string newState, string? lotId, string? recipeId, CancellationToken ct)
    {
        var prev = _currentState;
        _currentState = newState;
        _currentLot = lotId;
        _currentRecipe = recipeId;

        _logger.LogInformation("[{EqId}] State: {Prev} → {New}", _equipmentId, prev, Sanitize(newState));

        await _bus.PublishAsync(new EquipmentStateChangedEvent
        {
            EquipmentId = _equipmentId,
            EquipmentName = _equipmentName,
            PreviousState = prev,
            NewState = newState,
            RecipeId = recipeId,
            LotId = lotId
        }, ct);
    }

    public async Task RaiseAlarmAsync(string code, string level, string description, CancellationToken ct)
    {
        _logger.LogWarning("[{EqId}] Alarm raised: {Code} – {Desc}", _equipmentId, Sanitize(code), Sanitize(description));
        await _bus.PublishAsync(new AlarmRaisedEvent
        {
            EquipmentId = _equipmentId,
            AlarmCode = code,
            AlarmLevel = level,
            Description = description
        }, ct);
    }

    public async Task ClearAlarmAsync(string code, CancellationToken ct)
    {
        _logger.LogInformation("[{EqId}] Alarm cleared: {Code}", _equipmentId, Sanitize(code));
        await _bus.PublishAsync(new AlarmClearedEvent
        {
            EquipmentId = _equipmentId,
            AlarmCode = code
        }, ct);
    }

    public async Task EmitTestResultAsync(CancellationToken ct)
    {
        var sn = $"SN-{_equipmentId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{_snCounter++:D4}";
        var pass = _rng.Next(0, 10) > 1; // 80% pass rate

        var items = new List<TestItem>
        {
            MakeItem("Voltage",   4.95 + _rng.NextDouble() * 0.1, 4.80, 5.20, "V", pass),
            MakeItem("Current",   0.98 + _rng.NextDouble() * 0.04, 0.90, 1.10, "A", pass),
            MakeItem("Resistance",47.1 + _rng.NextDouble() * 0.5, 45.0, 49.0, "Ω", pass),
        };

        var verdict = items.All(i => i.Verdict == "PASS") ? "PASS" : "FAIL";

        _logger.LogInformation("[{EqId}] Test result: SN={SN} Verdict={Verdict}", _equipmentId, sn, verdict);

        await _bus.PublishAsync(new TestResultPublishedEvent
        {
            TestResult = new TestResult
            {
                SerialNumber = sn,
                LotId = _currentLot ?? "UNKNOWN",
                EquipmentId = _equipmentId,
                StationId = $"{_equipmentId}-ST1",
                TestProgram = _currentRecipe ?? "DEFAULT",
                Verdict = verdict,
                Items = items
            }
        }, ct);
    }

    private TestItem MakeItem(string name, double measured, double lo, double hi, string unit, bool forcePass)
    {
        if (!forcePass && _rng.Next(0, 10) < 2)
            measured = lo - 0.5; // intentionally fail

        return new TestItem
        {
            ItemName = name,
            MeasuredValue = Math.Round(measured, 4),
            LowerLimit = lo,
            UpperLimit = hi,
            Unit = unit,
            Verdict = (measured >= lo && measured <= hi) ? "PASS" : "FAIL"
        };
    }

    /// <summary>Removes newlines and control characters to prevent log injection.</summary>
    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
}
