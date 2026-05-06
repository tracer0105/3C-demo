namespace Cim.DbAdapter.Models;

public class TestResult
{
    public long Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string LotId { get; set; } = string.Empty;
    public string EquipmentId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string TestProgram { get; set; } = string.Empty;
    public string Verdict { get; set; } = string.Empty;    // PASS / FAIL / ABORT
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    public string? Operator { get; set; }
    public List<TestItem> Items { get; set; } = new();
}
