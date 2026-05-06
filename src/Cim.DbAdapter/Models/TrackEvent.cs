namespace Cim.DbAdapter.Models;

public class TrackEvent
{
    public long Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string LotId { get; set; } = string.Empty;
    public string EquipmentId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;  // TRACKIN / TRACKOUT
    public string? RecipeId { get; set; }
    public string? Operator { get; set; }
    public DateTime EventTime { get; set; } = DateTime.UtcNow;
    public string? Remarks { get; set; }
}
