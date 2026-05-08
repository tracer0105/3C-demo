namespace Cim.DbAdapter.Models;

public class Alarm
{
    public long Id { get; set; }
    public string EquipmentId { get; set; } = string.Empty;
    public string AlarmCode { get; set; } = string.Empty;
    public string AlarmLevel { get; set; } = string.Empty;  // WARNING / ERROR / CRITICAL
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;      // ACTIVE / CLEARED
    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClearedAt { get; set; }
}
