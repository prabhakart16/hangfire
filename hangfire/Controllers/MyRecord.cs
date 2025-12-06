namespace hangfire.Controllers
{
    public class MyRecord
    {
        public string BatchId { get; set; }
        public int ChunkNumber { get; set; }
        public int RowNumber { get; set; }
        public string CustomerName { get; set; }
        public string AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public bool HasError { get; internal set; }
    }
    public class DiscrepancyRecord
    {
        public int RowNumber { get; set; }
        public string CustomerName { get; set; }
        public string AccountNumber { get; set; }
        public decimal SystemAmount { get; set; }
        public decimal ExcelAmount { get; set; }
        public string Reason { get; set; }
    }
    public class UploadChunkRequest
    {
        public IFormFile File { get; set; }
        public string BatchId { get; set; }
        public int ChunkNumber { get; set; }
    }
}
