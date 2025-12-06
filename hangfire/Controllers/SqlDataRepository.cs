using Dapper;
using hangfire.Controllers;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.SqlClient;

namespace hangfire.Controllers
{
    public class SqlDataRepository : IDataRepository
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SqlDataRepository> _logger;
        private readonly string _connectionString;
        private readonly IBackgroundJobClient _backgroundJobs;

        public SqlDataRepository(
            IConfiguration config,
            ILogger<SqlDataRepository> logger,
            IBackgroundJobClient backgroundJobs)
        {
            _config = config;
            _logger = logger;
            _backgroundJobs = backgroundJobs;
            _connectionString = _config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'");
        }

        private SqlConnection GetConnection() => new SqlConnection(_connectionString);

        public async Task SaveChunkAsync(string batchId, int chunkNumber, List<MyRecord> records)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            await using var tran = await conn.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("🔹 Saving chunk {ChunkNumber} for batch {BatchId}", chunkNumber, batchId);

                // 1️⃣ Ensure batch exists
                await conn.ExecuteAsync(@"
                    IF NOT EXISTS (SELECT 1 FROM Batches WHERE BatchId = @BatchId)
                    BEGIN
                        INSERT INTO Batches (BatchId, TotalChunks, ReceivedChunks, ProcessedChunks, Status, CreatedAt)
                        VALUES (@BatchId, 0, 0, 0, 'Receiving', GETUTCDATE())
                    END",
                    new { BatchId = batchId }, tran);

                // 2️⃣ Ensure chunk record exists
                await conn.ExecuteAsync(@"
                    IF NOT EXISTS (SELECT 1 FROM BatchChunks WHERE BatchId = @BatchId AND ChunkNumber = @ChunkNumber)
                    BEGIN
                        INSERT INTO BatchChunks (BatchId, ChunkNumber, Status, ReceivedAt)
                        VALUES (@BatchId, @ChunkNumber, 'Received', GETUTCDATE())
                    END",
                    new { BatchId = batchId, ChunkNumber = chunkNumber }, tran);

                // 3️⃣ Save records using bulk copy
                using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, (SqlTransaction)tran))
                {
                    bulkCopy.DestinationTableName = "BulkUploadRecords";

                    var table = new DataTable();
                    table.Columns.Add("CustomerName", typeof(string));
                    table.Columns.Add("AccountNumber", typeof(string));
                    table.Columns.Add("Amount", typeof(decimal));
                    table.Columns.Add("BatchId", typeof(string));
                    table.Columns.Add("ChunkIndex", typeof(int));

                    foreach (var r in records)
                    {
                        table.Rows.Add(r.CustomerName, r.AccountNumber, r.Amount, batchId, chunkNumber);
                    }

                    await bulkCopy.WriteToServerAsync(table);
                }

                // 4️⃣ Mark chunk complete
                await conn.ExecuteAsync(@"
                    UPDATE BatchChunks
                    SET Status = 'Processed', IsCompleted = 1, CompletedAt = GETUTCDATE()
                    WHERE BatchId = @BatchId AND ChunkNumber = @ChunkNumber",
                    new { BatchId = batchId, ChunkNumber = chunkNumber }, tran);

                // 5️⃣ Update batch counters
                await conn.ExecuteAsync(@"
                    UPDATE Batches
                    SET ProcessedChunks = (
                        SELECT COUNT(*) FROM BatchChunks WHERE BatchId = @BatchId AND IsCompleted = 1
                    ),
                    ReceivedChunks = (
                        SELECT COUNT(*) FROM BatchChunks WHERE BatchId = @BatchId
                    )
                    WHERE BatchId = @BatchId",
                    new { BatchId = batchId }, tran);

                await tran.CommitAsync();

                _logger.LogInformation("✅ Chunk {ChunkNumber} saved successfully for batch {BatchId}", chunkNumber, batchId);

                // 6️⃣ Check if all chunks completed after commit
                if (await AllChunksCompletedAsync(batchId))
                {
                    _logger.LogInformation("🎯 All chunks completed for batch {BatchId}. Enqueuing final discrepancy report job...", batchId);

                    _backgroundJobs.Enqueue<IReportGenerator>(r =>
                        r.GenerateDiscrepancyReportAsync(batchId));

                    await using var updateConn = GetConnection();
                    await updateConn.OpenAsync();
                    await updateConn.ExecuteAsync(@"
                        UPDATE Batches
                        SET Status = 'Processing'
                        WHERE BatchId = @BatchId",
                        new { BatchId = batchId });
                }
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                _logger.LogError(ex, "❌ Error saving chunk {ChunkNumber} for batch {BatchId}", chunkNumber, batchId);
                throw;
            }
        }

        public async Task MarkChunkCompletedAsync(string batchId, int chunkNumber)
        {
            await using var conn = GetConnection();
            await conn.ExecuteAsync(@"
                UPDATE BatchChunks
                SET Status = 'Processed', IsCompleted = 1, CompletedAt = GETUTCDATE()
                WHERE BatchId = @BatchId AND ChunkNumber = @ChunkNumber",
                new { BatchId = batchId, ChunkNumber = chunkNumber });
        }

        public async Task<bool> AllChunksCompletedAsync(string batchId)
        {
            await using var conn = GetConnection();

            var result = await conn.QuerySingleAsync<int>(@"
                SELECT CASE 
                    WHEN COUNT(*) = SUM(CASE WHEN IsCompleted = 1 THEN 1 ELSE 0 END)
                    THEN 1 ELSE 0 END
                FROM BatchChunks
                WHERE BatchId = @BatchId",
                new { BatchId = batchId });

            return result == 1;
        }

        public async Task<List<MyRecord>> GetRecordsForBatchAsync(string batchId)
        {
            await using var conn = GetConnection();
            var data = await conn.QueryAsync<MyRecord>(@"
                SELECT CustomerName, AccountNumber, Amount
                FROM BulkUploadRecords
                WHERE BatchId = @BatchId",
                new { BatchId = batchId });

            return data.ToList();
        }
    }
}
