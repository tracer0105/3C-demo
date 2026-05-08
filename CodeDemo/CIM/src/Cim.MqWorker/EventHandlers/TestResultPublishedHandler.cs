using Cim.DbAdapter.Models;
using Cim.DbAdapter.Repositories;
using Microsoft.Extensions.Logging;

namespace Cim.MqWorker.EventHandlers;

/// <summary>Handles TestResultPublishedEvent – persists normalized test results.</summary>
public class TestResultPublishedHandler
{
    private readonly ITestResultRepository _repo;
    private readonly ILogger<TestResultPublishedHandler> _logger;

    public TestResultPublishedHandler(ITestResultRepository repo, ILogger<TestResultPublishedHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task HandleAsync(TestResultPublishedEvent evt, CancellationToken ct)
    {
        _logger.LogInformation("Test result for SN {SN}: {Verdict} ({Items} items)",
            evt.TestResult.SerialNumber, evt.TestResult.Verdict, evt.TestResult.Items.Count);

        await _repo.UpsertAsync(evt.TestResult, ct);
    }
}
