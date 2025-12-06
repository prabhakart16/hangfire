using hangfire.Controllers;

public interface IReportGenerator
{
    Task GenerateDiscrepancyReportAsync(string batchId);
}

public class ReportGenerator : IReportGenerator
{
    private readonly ILogger<ReportGenerator> _logger;
    private readonly IDataRepository _repo;

    public ReportGenerator(ILogger<ReportGenerator> logger, IDataRepository repo)
    {
        _logger = logger;
        _repo = repo;
    }

    public async Task GenerateDiscrepancyReportAsync(string batchId)
    {
        _logger.LogInformation("🔍 Starting discrepancy check for Batch {BatchId}", batchId);

        var records = await _repo.GetRecordsForBatchAsync(batchId);
        var discrepancies = new List<DiscrepancyRecord>();

        // 🔹 Example business rule for reconciliation:
        // Compare DB records vs Excel data logic here — assuming same dataset for now
        foreach (var rec in records)
        {
            // Example: system fetched balance or recalculated value
            var systemAmount = RecalculateSystemAmount(rec);

            if (systemAmount != rec.Amount)
            {
                discrepancies.Add(new DiscrepancyRecord
                {
                    RowNumber = rec.RowNumber,
                    CustomerName = rec.CustomerName,
                    AccountNumber = rec.AccountNumber,
                    SystemAmount = systemAmount,
                    ExcelAmount = rec.Amount,
                    Reason = $"Amount mismatch: Expected {systemAmount}, got {rec.Amount}"
                });
            }
        }

        // 🔹 Write Excel file
        var filePath = ExcelHelper.WriteDiscrepancyReport(batchId, discrepancies);

        _logger.LogInformation("✅ Discrepancy report generated for Batch {BatchId} at {Path}", batchId, filePath);
    }

    private decimal RecalculateSystemAmount(MyRecord record)
    {
        // Example rule: simulate system-side validation
        // Replace with real reconciliation logic (e.g., DB lookup, API, etc.)
        return record.Amount * 1.00m; // For demo, same as Excel amount
    }


}
