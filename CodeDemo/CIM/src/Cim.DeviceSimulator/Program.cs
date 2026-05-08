using Cim.DbAdapter.EventBus;
using Cim.DbAdapter.Models;
using Cim.DbAdapter.Repositories;
using Cim.DbAdapter.Schema;
using Cim.DeviceSimulator.Simulation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// ─── Configuration ─────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var dbPath = config["Database:Path"] ?? "./data/cim.db";
var tcpPort = int.Parse(config["TcpControlPort"] ?? "7001");
var connectionString = $"Data Source={dbPath}";
var cycleIntervalSec = int.Parse(config["CycleIntervalSeconds"] ?? "5");

// ─── Logging ────────────────────────────────────────────────────────────────
using var loggerFactory = LoggerFactory.Create(b =>
    b.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

// ─── DB Init ────────────────────────────────────────────────────────────────
await DbInitializer.InitializeAsync(connectionString, logger);

// ─── Shared services ────────────────────────────────────────────────────────
var eventBus = new InMemoryEventBus(loggerFactory.CreateLogger<InMemoryEventBus>());

// Repositories
IEquipmentRepository equipRepo   = new EquipmentRepository(connectionString);
ITrackEventRepository trackRepo  = new TrackEventRepository(connectionString);
ITestResultRepository resultRepo = new TestResultRepository(connectionString);
IAlarmRepository alarmRepo       = new AlarmRepository(connectionString);

// ─── Wire up inline event handlers (same role as MqWorker in production) ────
using var sub1 = eventBus.Subscribe<EquipmentStateChangedEvent>(async (evt, ct) =>
{
    var status = await equipRepo.GetByIdAsync(evt.EquipmentId, ct) ?? new EquipmentStatus
    {
        EquipmentId = evt.EquipmentId,
        EquipmentName = evt.EquipmentName
    };
    status.EquipmentName = evt.EquipmentName;
    status.State = evt.NewState;
    status.RecipeId = evt.RecipeId;
    status.LotId = evt.LotId;
    await equipRepo.UpsertAsync(status, ct);
});

using var sub2 = eventBus.Subscribe<AlarmRaisedEvent>(async (evt, ct) =>
{
    await alarmRepo.InsertAsync(new Alarm
    {
        EquipmentId = evt.EquipmentId,
        AlarmCode = evt.AlarmCode,
        AlarmLevel = evt.AlarmLevel,
        Description = evt.Description
    }, ct);
});

using var sub3 = eventBus.Subscribe<AlarmClearedEvent>(async (evt, ct) =>
{
    await alarmRepo.ClearAlarmAsync(evt.EquipmentId, evt.AlarmCode, ct);
});

using var sub4 = eventBus.Subscribe<TestResultPublishedEvent>(async (evt, ct) =>
{
    await resultRepo.UpsertAsync(evt.TestResult, ct);
});

// ─── Equipment simulators ───────────────────────────────────────────────────
var simulators = new Dictionary<string, EquipmentSimulator>(StringComparer.OrdinalIgnoreCase)
{
    ["SMT-01"]  = new("SMT-01",  "SMT Line 1",  eventBus, loggerFactory.CreateLogger<EquipmentSimulator>()),
    ["AOI-01"]  = new("AOI-01",  "AOI Station", eventBus, loggerFactory.CreateLogger<EquipmentSimulator>()),
    ["TEST-01"] = new("TEST-01", "FCT Tester",  eventBus, loggerFactory.CreateLogger<EquipmentSimulator>()),
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

logger.LogInformation("Device Simulator starting. Press Ctrl+C to stop.");
logger.LogInformation("DB path: {DbPath} | TCP control port: {Port} | Cycle: {Sec}s", dbPath, tcpPort, cycleIntervalSec);

// ─── Start TCP control server ───────────────────────────────────────────────
var tcpServer = new TcpControlServer(tcpPort, simulators, loggerFactory.CreateLogger<TcpControlServer>());
var tcpTask = tcpServer.RunAsync(cts.Token);

// ─── Main simulation loop ───────────────────────────────────────────────────
while (!cts.Token.IsCancellationRequested)
{
    var tasks = simulators.Values
        .Select(sim => sim.RunCycleAsync(cts.Token))
        .ToArray();

    try
    {
        await Task.WhenAll(tasks);
        await Task.Delay(TimeSpan.FromSeconds(cycleIntervalSec), cts.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

logger.LogInformation("Device Simulator stopped.");
await tcpTask;
await eventBus.DisposeAsync();
