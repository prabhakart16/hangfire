using hangfire.Controllers;
using Hangfire;

public interface IChunkProcessor
{
    Task ProcessChunkAsync(string batchId, int chunkNumber, string filePath);
}
public class ChunkUploadRequest
{
    public string BatchId { get; set; } = default!;
    public int ChunkNumber { get; set; }
    public List<MyRecord> Records { get; set; } = new();
}
public class ChunkProcessor : IChunkProcessor
{
    private readonly ILogger<ChunkProcessor> _logger;
    private readonly IDataRepository _repo;
    private readonly IBackgroundJobClient _backgroundJobs;

    public ChunkProcessor(ILogger<ChunkProcessor> logger, IDataRepository repo, IBackgroundJobClient backgroundJobs)
    {
        _logger = logger;
        _repo = repo;
        _backgroundJobs = backgroundJobs;
    }

    public async Task ProcessChunkAsync(string batchId, int chunkNumber, string filePath)
    {
        _logger.LogInformation("Processing chunk {chunk} for batch {batch}", chunkNumber, batchId);

        // Parse Excel (e.g., using EPPlus or ClosedXML)
        var records = ExcelHelper.ReadData(filePath);

        // Store in DB
        await _repo.SaveChunkAsync(batchId, chunkNumber, records);

        // Mark this chunk as processed
        await _repo.MarkChunkCompletedAsync(batchId, chunkNumber);

        // Check if all chunks are completed
        if (await _repo.AllChunksCompletedAsync(batchId))
        {
            // enqueue final consolidation job
            _backgroundJobs.Enqueue<IReportGenerator>(r => r.GenerateDiscrepancyReportAsync(batchId));
        }

        _logger.LogInformation("Completed chunk {chunk}", chunkNumber);
    }
}
