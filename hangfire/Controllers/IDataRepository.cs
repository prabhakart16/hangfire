namespace hangfire.Controllers
{
    public interface IDataRepository
    {
        Task SaveChunkAsync(string batchId, int chunkNumber, List<MyRecord> records);
        Task MarkChunkCompletedAsync(string batchId, int chunkNumber);
        Task<bool> AllChunksCompletedAsync(string batchId);
        Task<List<MyRecord>> GetRecordsForBatchAsync(string batchId);
    }

}
