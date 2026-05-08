using Cim.DbAdapter.EventBus;
using Cim.DbAdapter.Repositories;
using Cim.DbAdapter.Schema;
using Cim.MqWorker;
using Cim.MqWorker.EventHandlers;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        var dbPath = ctx.Configuration["Database:Path"] ?? "./data/cim.db";
        var connectionString = $"Data Source={dbPath}";

        // Repositories
        services.AddSingleton<IEquipmentRepository>(_ => new EquipmentRepository(connectionString));
        services.AddSingleton<ITrackEventRepository>(_ => new TrackEventRepository(connectionString));
        services.AddSingleton<ITestResultRepository>(_ => new TestResultRepository(connectionString));
        services.AddSingleton<IAlarmRepository>(_ => new AlarmRepository(connectionString));

        // Event bus – singleton so the same bus instance is shared within the process
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        // Event handlers
        services.AddSingleton<EquipmentStateChangedHandler>();
        services.AddSingleton<AlarmRaisedHandler>();
        services.AddSingleton<AlarmClearedHandler>();
        services.AddSingleton<TestResultPublishedHandler>();

        // The hosted worker
        services.AddHostedService<CimWorker>();
    });

var host = builder.Build();

// Initialize DB schema before running
var dbPathConfig = host.Services.GetRequiredService<IConfiguration>()["Database:Path"] ?? "./data/cim.db";
await DbInitializer.InitializeAsync(
    $"Data Source={dbPathConfig}",
    host.Services.GetRequiredService<ILogger<Program>>());

await host.RunAsync();
