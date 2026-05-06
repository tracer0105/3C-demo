using Cim.DbAdapter.EventBus;
using Cim.DbAdapter.Models;
using Cim.DbAdapter.Repositories;
using Cim.DbAdapter.Schema;
using Cim.RestApi.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ─────────────────────────────────────────────────────────
var dbPath = builder.Configuration["Database:Path"] ?? "./data/cim.db";
var connectionString = $"Data Source={dbPath}";

// ─── Services ──────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "3C CIM Integration API",
        Version = "v1",
        Description = "REST API for 3C industry CIM integration demo"
    });
    c.AddSecurityDefinition("IdempotencyKey", new OpenApiSecurityScheme
    {
        Name = "Idempotency-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Optional idempotency key for POST requests"
    });
});

// Repositories – simple constructor injection
builder.Services.AddSingleton<IEquipmentRepository>(_ => new EquipmentRepository(connectionString));
builder.Services.AddSingleton<ITrackEventRepository>(_ => new TrackEventRepository(connectionString));
builder.Services.AddSingleton<ITestResultRepository>(_ => new TestResultRepository(connectionString));
builder.Services.AddSingleton<IAlarmRepository>(_ => new AlarmRepository(connectionString));

// Shared event bus (singleton so RestApi, Worker and Simulator all share it in the same process)
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

builder.Services.AddLogging();

var app = builder.Build();

// ─── DB Init ───────────────────────────────────────────────────────────────
await DbInitializer.InitializeAsync(connectionString, app.Logger);

// ─── Middleware ─────────────────────────────────────────────────────────────
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        var feature = ctx.Features.Get<IExceptionHandlerFeature>();
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = "InternalServerError",
            message = feature?.Error.Message ?? "An unexpected error occurred."
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "3C CIM API v1"));
}

app.UseMiddleware<IdempotencyMiddleware>();

// ─── Endpoints ──────────────────────────────────────────────────────────────

// GET /api/equipment/{equipmentId}/status
app.MapGet("/api/equipment/{equipmentId}/status", async (
    string equipmentId,
    IEquipmentRepository repo,
    CancellationToken ct) =>
{
    var status = await repo.GetByIdAsync(equipmentId, ct);
    return status is null
        ? Results.NotFound(new { error = "NotFound", message = $"Equipment '{equipmentId}' not found." })
        : Results.Ok(status);
})
.WithName("GetEquipmentStatus")
.WithOpenApi(op => { op.Summary = "Get current equipment status"; return op; });

// POST /api/recipe/verify
app.MapPost("/api/recipe/verify", async (
    RecipeVerifyRequest request,
    IEquipmentRepository repo,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.EquipmentId) || string.IsNullOrWhiteSpace(request.RecipeId))
        return Results.BadRequest(new { error = "InvalidRequest", message = "EquipmentId and RecipeId are required." });

    var equipment = await repo.GetByIdAsync(request.EquipmentId, ct);
    if (equipment is null)
        return Results.NotFound(new { error = "NotFound", message = $"Equipment '{request.EquipmentId}' not found." });

    // Simulate recipe verification logic
    var isCompatible = !request.RecipeId.StartsWith("INVALID");
    return Results.Ok(new
    {
        EquipmentId = request.EquipmentId,
        RecipeId = request.RecipeId,
        Compatible = isCompatible,
        Message = isCompatible ? "Recipe verified successfully." : "Recipe is not compatible with this equipment.",
        VerifiedAt = DateTime.UtcNow
    });
})
.WithName("VerifyRecipe")
.WithOpenApi(op => { op.Summary = "Verify recipe compatibility with equipment"; return op; });

// POST /api/trackin
app.MapPost("/api/trackin", async (
    TrackInRequest request,
    ITrackEventRepository trackRepo,
    IEquipmentRepository equipRepo,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.SerialNumber) || string.IsNullOrWhiteSpace(request.EquipmentId))
        return Results.BadRequest(new { error = "InvalidRequest", message = "SerialNumber and EquipmentId are required." });

    var trackEvent = new TrackEvent
    {
        SerialNumber = request.SerialNumber,
        LotId = request.LotId ?? string.Empty,
        EquipmentId = request.EquipmentId,
        StationId = request.StationId ?? request.EquipmentId,
        EventType = "TRACKIN",
        RecipeId = request.RecipeId,
        Operator = request.Operator,
        Remarks = request.Remarks
    };

    var id = await trackRepo.InsertAsync(trackEvent, ct);

    // Update equipment status
    var status = await equipRepo.GetByIdAsync(request.EquipmentId, ct) ?? new EquipmentStatus
    {
        EquipmentId = request.EquipmentId,
        EquipmentName = request.EquipmentId
    };
    status.State = "RUNNING";
    status.RecipeId = request.RecipeId;
    status.LotId = request.LotId;
    await equipRepo.UpsertAsync(status, ct);

    return Results.Ok(new { EventId = id, Message = "TrackIn recorded.", EventTime = trackEvent.EventTime });
})
.WithName("TrackIn")
.WithOpenApi(op => { op.Summary = "Record a TrackIn event (SN enters a station)"; return op; });

// POST /api/trackout
app.MapPost("/api/trackout", async (
    TrackOutRequest request,
    ITrackEventRepository trackRepo,
    IEquipmentRepository equipRepo,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.SerialNumber) || string.IsNullOrWhiteSpace(request.EquipmentId))
        return Results.BadRequest(new { error = "InvalidRequest", message = "SerialNumber and EquipmentId are required." });

    var trackEvent = new TrackEvent
    {
        SerialNumber = request.SerialNumber,
        LotId = request.LotId ?? string.Empty,
        EquipmentId = request.EquipmentId,
        StationId = request.StationId ?? request.EquipmentId,
        EventType = "TRACKOUT",
        RecipeId = request.RecipeId,
        Operator = request.Operator,
        Remarks = request.Remarks
    };

    var id = await trackRepo.InsertAsync(trackEvent, ct);

    // Update equipment status to IDLE after trackout
    var status = await equipRepo.GetByIdAsync(request.EquipmentId, ct) ?? new EquipmentStatus
    {
        EquipmentId = request.EquipmentId,
        EquipmentName = request.EquipmentId
    };
    status.State = "IDLE";
    status.LotId = null;
    await equipRepo.UpsertAsync(status, ct);

    return Results.Ok(new { EventId = id, Message = "TrackOut recorded.", EventTime = trackEvent.EventTime });
})
.WithName("TrackOut")
.WithOpenApi(op => { op.Summary = "Record a TrackOut event (SN leaves a station)"; return op; });

// POST /api/testresults/upsert
app.MapPost("/api/testresults/upsert", async (
    TestResultUpsertRequest request,
    ITestResultRepository repo,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.SerialNumber))
        return Results.BadRequest(new { error = "InvalidRequest", message = "SerialNumber is required." });

    var result = new TestResult
    {
        SerialNumber = request.SerialNumber,
        LotId = request.LotId ?? string.Empty,
        EquipmentId = request.EquipmentId ?? string.Empty,
        StationId = request.StationId ?? string.Empty,
        TestProgram = request.TestProgram ?? string.Empty,
        Verdict = request.Verdict,
        Operator = request.Operator,
        Items = request.Items?.Select(i => new TestItem
        {
            ItemName = i.ItemName,
            MeasuredValue = i.MeasuredValue,
            LowerLimit = i.LowerLimit,
            UpperLimit = i.UpperLimit,
            Unit = i.Unit,
            Verdict = i.Verdict
        }).ToList() ?? new List<TestItem>()
    };

    var id = await repo.UpsertAsync(result, ct);
    return Results.Ok(new { ResultId = id, Message = "Test result upserted.", TestedAt = result.TestedAt });
})
.WithName("UpsertTestResult")
.WithOpenApi(op => { op.Summary = "Upsert a test result (idempotent by SN+StationId)"; return op; });

// GET /api/testresults/{sn}
app.MapGet("/api/testresults/{sn}", async (
    string sn,
    ITestResultRepository repo,
    CancellationToken ct) =>
{
    var result = await repo.GetBySerialNumberAsync(sn, ct);
    return result is null
        ? Results.NotFound(new { error = "NotFound", message = $"No test result found for SN '{sn}'." })
        : Results.Ok(result);
})
.WithName("GetTestResult")
.WithOpenApi(op => { op.Summary = "Query the latest test result for a serial number"; return op; });

app.Run();

// ─── Request DTOs ───────────────────────────────────────────────────────────
record RecipeVerifyRequest(string EquipmentId, string RecipeId);

record TrackInRequest(
    string SerialNumber,
    string? LotId,
    string EquipmentId,
    string? StationId,
    string? RecipeId,
    string? Operator,
    string? Remarks);

record TrackOutRequest(
    string SerialNumber,
    string? LotId,
    string EquipmentId,
    string? StationId,
    string? RecipeId,
    string? Operator,
    string? Remarks);

record TestResultUpsertRequest(
    string SerialNumber,
    string? LotId,
    string? EquipmentId,
    string? StationId,
    string? TestProgram,
    string Verdict,
    string? Operator,
    List<TestItemRequest>? Items);

record TestItemRequest(
    string ItemName,
    double? MeasuredValue,
    double? LowerLimit,
    double? UpperLimit,
    string? Unit,
    string Verdict);
