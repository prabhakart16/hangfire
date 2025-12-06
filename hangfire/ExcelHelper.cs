using ClosedXML.Excel;
using hangfire.Controllers;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace hangfire.Controllers
{
    public static class ExcelHelper
    {
        // 🧩 Read Excel into List<MyRecord>
        public static List<MyRecord> ReadRecordsFromExcel(Stream excelStream, string batchId, int chunkNumber)
        {
            var records = new List<MyRecord>();

            using (var workbook = new XLWorkbook(excelStream))
            {
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // skip header row
                int rowNumber = 1;

                foreach (var row in rows)
                {
                    records.Add(new MyRecord
                    {
                        BatchId = batchId,
                        ChunkNumber = chunkNumber,
                        RowNumber = rowNumber++,
                        CustomerName = row.Cell(1).GetString(),
                        AccountNumber = row.Cell(2).GetString(),
                        Amount = Convert.ToDecimal(row.Cell(3).GetString()),
                        Status = "Pending"
                    });
                }
            }

            return records;
        }
        public static List<MyRecord> ReadData(string filePath)
        {
            var records = new List<MyRecord>();

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Excel file not found: {filePath}");

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1); // reads first worksheet
            var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // skip header row

            int rowNumber = 1;
            foreach (var row in rows)
            {
                try
                {
                    var record = new MyRecord
                    {
                        CustomerName = row.Cell(1).GetString().Trim(),
                        AccountNumber = row.Cell(2).GetString().Trim(),
                        Amount = decimal.TryParse(row.Cell(3).GetString(), out var amt) ? amt : 0,
                        RowNumber = rowNumber++,
                        Status = "Pending"
                    };

                    records.Add(record);
                }
                catch (Exception ex)
                {
                    // You can log this if needed, e.g., invalid row data
                    Console.WriteLine($"⚠️ Error reading row {rowNumber}: {ex.Message}");
                }
            }

            return records;
        }

        // 🧩 Write Discrepancy Report to Excel
        public static string WriteDiscrepancyReport(string batchId, List<DiscrepancyRecord> discrepancies)
        {
            var reportsDir = Path.Combine("Reports");
            Directory.CreateDirectory(reportsDir);
            var filePath = Path.Combine(reportsDir, $"DiscrepancyReport_{batchId}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Discrepancies");

                // Header
                ws.Cell(1, 1).Value = "RowNumber";
                ws.Cell(1, 2).Value = "CustomerName";
                ws.Cell(1, 3).Value = "AccountNumber";
                ws.Cell(1, 4).Value = "Amount (System)";
                ws.Cell(1, 5).Value = "Amount (Excel)";
                ws.Cell(1, 6).Value = "DiscrepancyReason";

                // Data
                int row = 2;
                foreach (var d in discrepancies)
                {
                    ws.Cell(row, 1).Value = d.RowNumber;
                    ws.Cell(row, 2).Value = d.CustomerName;
                    ws.Cell(row, 3).Value = d.AccountNumber;
                    ws.Cell(row, 4).Value = d.SystemAmount;
                    ws.Cell(row, 5).Value = d.ExcelAmount;
                    ws.Cell(row, 6).Value = d.Reason;
                    row++;
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }

            return filePath;
        }
    }
}
