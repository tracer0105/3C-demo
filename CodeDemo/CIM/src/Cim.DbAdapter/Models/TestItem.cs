namespace Cim.DbAdapter.Models;

public class TestItem
{
    public long Id { get; set; }
    public long TestResultId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double? MeasuredValue { get; set; }
    public double? LowerLimit { get; set; }
    public double? UpperLimit { get; set; }
    public string? Unit { get; set; }
    public string Verdict { get; set; } = string.Empty;  // PASS / FAIL
}
