using Cim.DbAdapter.Models;

namespace Cim.DbAdapter.Repositories;

public interface ITestResultRepository
{
    Task<long> UpsertAsync(TestResult result, CancellationToken ct = default);
    Task<TestResult?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default);
    Task<IEnumerable<TestResult>> GetByLotIdAsync(string lotId, CancellationToken ct = default);
}
