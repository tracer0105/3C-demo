namespace Cim.DbAdapter.Models;

// ─── Event messages shared between DeviceSimulator, MqWorker and RestApi ──────

public abstract class CimEventMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class EquipmentStateChangedEvent : CimEventMessage
{
    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
    public string? RecipeId { get; set; }
    public string? LotId { get; set; }
}

public class AlarmRaisedEvent : CimEventMessage
{
    public string EquipmentId { get; set; } = string.Empty;
    public string AlarmCode { get; set; } = string.Empty;
    public string AlarmLevel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class AlarmClearedEvent : CimEventMessage
{
    public string EquipmentId { get; set; } = string.Empty;
    public string AlarmCode { get; set; } = string.Empty;
}

public class TestResultPublishedEvent : CimEventMessage
{
    public TestResult TestResult { get; set; } = new();
}
