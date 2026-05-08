namespace Cim.DbAdapter.Models;

public class EquipmentStatus
{
    public long Id { get; set; }
    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;   // IDLE / RUNNING / ALARM / MAINTENANCE / DOWN
    public string? RecipeId { get; set; }
    public string? LotId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
