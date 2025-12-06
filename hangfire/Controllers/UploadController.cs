using hangfire.Controllers;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<UploadController> _logger;

    public UploadController(
        IDataRepository repository,
        IBackgroundJobClient backgroundJobs,
        ILogger<UploadController> logger)
    {
        _repository = repository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    [HttpPost("upload-excel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadExcel([FromForm] UploadChunkRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("Invalid file.");

        using var stream = request.File.OpenReadStream();
        var records = ExcelHelper.ReadRecordsFromExcel(stream, request.BatchId, request.ChunkNumber);

        await _repository.SaveChunkAsync(request.BatchId, request.ChunkNumber, records);
        await _repository.MarkChunkCompletedAsync(request.BatchId, request.ChunkNumber);

        if (await _repository.AllChunksCompletedAsync(request.BatchId))
        {
            _backgroundJobs.Enqueue<ReportGenerator>(r => r.GenerateDiscrepancyReportAsync(request.BatchId));
        }

        return Ok(new { message = "Chunk uploaded successfully" });
    }
}
