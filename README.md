# 3C Industry CIM Integration Demo

A .NET 8 solution simulating a **3C (Consumer Electronics / Computer / Communication) shop-floor CIM integration** with REST API, message worker, SQLite persistence, and device simulation.

## Solution Structure

```
Cim3CIntegrationDemo.sln
└── src/
    ├── Cim.DbAdapter/          Class library – SQLite/Dapper persistence, shared models, in-memory event bus
    ├── Cim.RestApi/            ASP.NET Core Minimal API – CIM integration endpoints + Swagger
    ├── Cim.MqWorker/           Worker Service – event consumption, normalized DB writes
    └── Cim.DeviceSimulator/    Console App – shop-floor equipment/PLC simulation, TCP control port
```

## Architecture

```
┌─────────────────────────────────────────────────┐
│  3C Shop Floor (simulated)                       │
│  Cim.DeviceSimulator                             │
│  ┌──────────────┐  ┌──────────┐  ┌───────────┐  │
│  │ SMT-01       │  │ AOI-01   │  │ TEST-01   │  │
│  └──────┬───────┘  └────┬─────┘  └─────┬─────┘  │
│         └───────────────┼──────────────┘         │
│                         │ IEventBus.Publish       │
│  TCP Control Port :7001 │                         │
└─────────────────────────┼───────────────────────-┘
                          │
              ┌───────────▼──────────┐
              │ InMemoryEventBus     │  (swap → RabbitMQ/Kafka)
              └───────────┬──────────┘
                          │ Subscribe
              ┌───────────▼──────────┐
              │ Cim.MqWorker         │
              │  - EquipmentHandler  │
              │  - AlarmHandler      │──► Cim.DbAdapter (SQLite)
              │  - TestResultHandler │
              └──────────────────────┘
                          │
              ┌───────────▼──────────┐
              │ Cim.RestApi :5100    │──► Cim.DbAdapter (SQLite)
              │  GET /api/equipment  │
              │  POST /api/trackin   │
              │  POST /api/trackout  │
              │  POST /api/recipe    │
              │  POST /api/testresult│
              │  GET  /api/testresult│
              └──────────────────────┘
```

## Prerequisites

- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

## Quick Start

### 1. Build the solution

```bash
dotnet build
```

### 2. Start MqWorker (creates DB schema)

```bash
cd src/Cim.MqWorker
dotnet run
```

### 3. Start RestApi

```bash
cd src/Cim.RestApi
dotnet run
# Swagger UI: http://localhost:5100/swagger
```

### 4. Start DeviceSimulator

```bash
cd src/Cim.DeviceSimulator
dotnet run
```

> **Note:** All three components share the same SQLite DB (default `./data/cim.db`).  
> In production mode, MqWorker and DeviceSimulator would connect to a shared message broker (RabbitMQ/Kafka).

## Configuration

### Environment Variables

| Variable | Default | Description |
|---|---|---|
| `Database__Path` | `./data/cim.db` | Path to the SQLite database file |
| `Urls` | `http://localhost:5100` | REST API listen URL (RestApi only) |
| `TcpControlPort` | `7001` | TCP control port (DeviceSimulator only) |
| `CycleIntervalSeconds` | `5` | Simulation cycle interval in seconds |

### appsettings.json

Each project has an `appsettings.json`. Override with environment variables or `appsettings.{Environment}.json`.

## REST API Endpoints

Swagger UI: **http://localhost:5100/swagger**

| Method | Path | Description |
|---|---|---|
| GET | `/api/equipment/{equipmentId}/status` | Get current equipment status |
| POST | `/api/recipe/verify` | Verify recipe compatibility |
| POST | `/api/trackin` | Record a TrackIn event |
| POST | `/api/trackout` | Record a TrackOut event |
| POST | `/api/testresults/upsert` | Upsert test result (idempotent) |
| GET | `/api/testresults/{sn}` | Query test result by serial number |

### Idempotency

For `POST` endpoints, include `Idempotency-Key: <uuid>` in the request header. Duplicate requests with the same key return the cached response without re-execution.

## Sample curl Commands

### Get equipment status
```bash
curl -s http://localhost:5100/api/equipment/SMT-01/status | jq
```

### Verify recipe
```bash
curl -s -X POST http://localhost:5100/api/recipe/verify \
  -H "Content-Type: application/json" \
  -d '{"equipmentId":"SMT-01","recipeId":"RCP-001"}' | jq
```

### TrackIn
```bash
curl -s -X POST http://localhost:5100/api/trackin \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: trackin-001" \
  -d '{
    "serialNumber": "SN-TEST-001",
    "lotId": "LOT-20240101",
    "equipmentId": "SMT-01",
    "stationId": "SMT-01-ST1",
    "recipeId": "RCP-001",
    "operator": "OP001"
  }' | jq
```

### TrackOut
```bash
curl -s -X POST http://localhost:5100/api/trackout \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: trackout-001" \
  -d '{
    "serialNumber": "SN-TEST-001",
    "lotId": "LOT-20240101",
    "equipmentId": "SMT-01",
    "stationId": "SMT-01-ST1",
    "operator": "OP001"
  }' | jq
```

### Upsert test result
```bash
curl -s -X POST http://localhost:5100/api/testresults/upsert \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: result-sn001-st1" \
  -d '{
    "serialNumber": "SN-TEST-001",
    "lotId": "LOT-20240101",
    "equipmentId": "TEST-01",
    "stationId": "TEST-01-ST1",
    "testProgram": "FCT-v1.0",
    "verdict": "PASS",
    "operator": "OP001",
    "items": [
      {"itemName":"Voltage","measuredValue":5.01,"lowerLimit":4.80,"upperLimit":5.20,"unit":"V","verdict":"PASS"},
      {"itemName":"Current","measuredValue":0.99,"lowerLimit":0.90,"upperLimit":1.10,"unit":"A","verdict":"PASS"}
    ]
  }' | jq
```

### Query test result by serial number
```bash
curl -s http://localhost:5100/api/testresults/SN-TEST-001 | jq
```

## TCP Control Port (DeviceSimulator)

Connect via telnet or nc:

```bash
nc 127.0.0.1 7001
```

Available commands:

```
STATE <equipmentId> <state>                   Force state transition
ALARM <equipmentId> <code> <level> <description>  Raise an alarm
CLEAR <equipmentId> <code>                    Clear an alarm
TEST  <equipmentId>                           Emit a test result
STATUS                                        List all equipment states
QUIT                                          Close connection
```

Examples:
```
STATE SMT-01 MAINTENANCE
ALARM AOI-01 ALM-002 ERROR Camera focus failed
CLEAR AOI-01 ALM-002
TEST TEST-01
STATUS
```

## Database Schema

SQLite database at `./data/cim.db` (relative to working directory of each process):

| Table | Description |
|---|---|
| `EquipmentStatus` | Current state of each equipment (upsert by EquipmentId) |
| `TrackEvents` | TRACKIN/TRACKOUT history |
| `TestResults` | Test result headers (upsert by SerialNumber+StationId) |
| `TestItems` | Individual test measurements per result |
| `Alarms` | Alarm history (ACTIVE/CLEARED) |

## Extending to Real Message Broker

Replace `InMemoryEventBus` with a RabbitMQ or Kafka implementation:

```csharp
// In DI registration:
services.AddSingleton<IEventBus, RabbitMqEventBus>();  // your implementation
```

The `IEventBus` interface (in `Cim.DbAdapter.EventBus`) has only two methods:
- `PublishAsync<T>(T message, CancellationToken ct)`
- `Subscribe<T>(Func<T, CancellationToken, Task> handler)`

## Project Dependency Graph

```
Cim.RestApi ──────┐
Cim.MqWorker ─────┤──► Cim.DbAdapter
Cim.DeviceSimulator ┘
```
