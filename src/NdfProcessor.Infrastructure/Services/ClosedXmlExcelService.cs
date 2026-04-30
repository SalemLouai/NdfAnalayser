using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NdfProcessor.Domain.Entities;
using NdfProcessor.Domain.Interfaces;
using NdfProcessor.Infrastructure.Configuration;

namespace NdfProcessor.Infrastructure.Services;

/// <summary>
/// Excel operations using ClosedXML
/// </summary>
public class ClosedXmlExcelService : IExcelService
{
    private readonly AppSettings _settings;
    private readonly ILogger<ClosedXmlExcelService> _logger;

    public ClosedXmlExcelService(IOptions<AppSettings> settings, ILogger<ClosedXmlExcelService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InsertReceiptAsync(Receipt receipt, CancellationToken cancellationToken = default)
    {
        var excelPath = GetExcelFilePath(receipt.Date.Year);

        await Task.Run(() =>
        {
            using var workbook = LoadOrCreateWorkbook(excelPath);
            var sheetName = receipt.Date.Month.ToString("D2");

            // Get or create worksheet for the month
            var worksheet = workbook.Worksheets.Contains(sheetName)
                ? workbook.Worksheet(sheetName)
                : CreateWorksheet(workbook, sheetName);

            // Find first empty row
            var row = FindFirstEmptyRow(worksheet);

            // Write data
            worksheet.Cell(row, _settings.Excel.Columns.Date).Value = receipt.Date.ToString("dd/MM/yyyy");
            worksheet.Cell(row, _settings.Excel.Columns.Time).Value = receipt.Time?.ToString(@"hh\:mm") ?? "";
            worksheet.Cell(row, _settings.Excel.Columns.Restaurant).Value = receipt.Restaurant;

            if (receipt.AmountExcludingTax.HasValue)
                worksheet.Cell(row, _settings.Excel.Columns.AmountExcludingTax).Value = receipt.AmountExcludingTax.Value;

            if (receipt.Tax.HasValue)
                worksheet.Cell(row, _settings.Excel.Columns.Tax).Value = receipt.Tax.Value;

            worksheet.Cell(row, _settings.Excel.Columns.TotalAmount).Value = receipt.TotalAmount;

            // Write file path for verification
            if (!string.IsNullOrEmpty(receipt.ProcessedFilePath))
                worksheet.Cell(row, _settings.Excel.Columns.FilePath).Value = receipt.ProcessedFilePath;

            // Format currency columns
            FormatCurrencyColumn(worksheet, row, _settings.Excel.Columns.AmountExcludingTax);
            FormatCurrencyColumn(worksheet, row, _settings.Excel.Columns.Tax);
            FormatCurrencyColumn(worksheet, row, _settings.Excel.Columns.TotalAmount);

            workbook.Save();
            _logger.LogInformation("Inserted receipt into Excel: {Date} {Restaurant} ${Amount}",
                receipt.Date, receipt.Restaurant, receipt.TotalAmount);
        }, cancellationToken);
    }

    public async Task<List<Receipt>> LoadExistingReceiptsAsync(CancellationToken cancellationToken = default)
    {
        var currentYear = DateTime.Now.Year;
        var excelPath = GetExcelFilePath(currentYear);

        if (!File.Exists(excelPath))
        {
            _logger.LogInformation("Excel file does not exist: {Path}", excelPath);
            return new List<Receipt>();
        }

        return await Task.Run(() =>
        {
            var receipts = new List<Receipt>();

            using var workbook = new XLWorkbook(excelPath);

            foreach (var worksheet in workbook.Worksheets)
            {
                var row = _settings.Excel.StartRow;
                while (!worksheet.Cell(row, _settings.Excel.Columns.Date).IsEmpty())
                {
                    try
                    {
                        var receipt = ParseReceiptFromRow(worksheet, row);
                        receipts.Add(receipt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing row {Row} in sheet {Sheet}", row, worksheet.Name);
                    }

                    row++;
                }
            }

            _logger.LogInformation("Loaded {Count} existing receipts from Excel", receipts.Count);
            return receipts;
        }, cancellationToken);
    }

    public string GetExcelFilePath(int year)
    {
        return _settings.Paths.ExcelFilePath.Replace("{year}", year.ToString());
    }

    private XLWorkbook LoadOrCreateWorkbook(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                return new XLWorkbook(path);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Excel file is locked or inaccessible: {Path}", path);
                throw new InvalidOperationException($"Cannot access Excel file. Please close it if open: {path}", ex);
            }
        }

        _logger.LogInformation("Creating new Excel file: {Path}", path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return new XLWorkbook();
    }

    private IXLWorksheet CreateWorksheet(XLWorkbook workbook, string sheetName)
    {
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Add headers
        worksheet.Cell(1, _settings.Excel.Columns.Date).Value = "Date";
        worksheet.Cell(1, _settings.Excel.Columns.Time).Value = "Time";
        worksheet.Cell(1, _settings.Excel.Columns.Restaurant).Value = "Restaurant";
        worksheet.Cell(1, _settings.Excel.Columns.AmountExcludingTax).Value = "Amount Excl. Tax";
        worksheet.Cell(1, _settings.Excel.Columns.Tax).Value = "Tax";
        worksheet.Cell(1, _settings.Excel.Columns.TotalAmount).Value = "Total Amount";
        worksheet.Cell(1, _settings.Excel.Columns.FilePath).Value = "File Path";

        // Bold headers
        worksheet.Row(1).Style.Font.Bold = true;

        _logger.LogInformation("Created new worksheet: {SheetName}", sheetName);
        return worksheet;
    }

    private int FindFirstEmptyRow(IXLWorksheet worksheet)
    {
        var row = _settings.Excel.StartRow;
        while (!worksheet.Cell(row, _settings.Excel.Columns.Date).IsEmpty())
        {
            row++;
        }
        return row;
    }

    private static void FormatCurrencyColumn(IXLWorksheet worksheet, int row, string column)
    {
        worksheet.Cell(row, column).Style.NumberFormat.Format = "#,##0.00 €";
    }

    private Receipt ParseReceiptFromRow(IXLWorksheet worksheet, int row)
    {
        var dateCell = worksheet.Cell(row, _settings.Excel.Columns.Date).GetString();
        var timeCell = worksheet.Cell(row, _settings.Excel.Columns.Time).GetString();
        var restaurant = worksheet.Cell(row, _settings.Excel.Columns.Restaurant).GetString();
        var filePathCell = worksheet.Cell(row, _settings.Excel.Columns.FilePath).GetString();

        var amountExclTaxCell = worksheet.Cell(row, _settings.Excel.Columns.AmountExcludingTax);
        var taxCell = worksheet.Cell(row, _settings.Excel.Columns.Tax);
        var totalAmountCell = worksheet.Cell(row, _settings.Excel.Columns.TotalAmount);

        return new Receipt
        {
            Date = DateTime.Parse(dateCell),
            Time = string.IsNullOrWhiteSpace(timeCell) ? null : TimeSpan.Parse(timeCell),
            Restaurant = restaurant,
            AmountExcludingTax = amountExclTaxCell.IsEmpty() ? null : (decimal?)amountExclTaxCell.GetValue<decimal>(),
            Tax = taxCell.IsEmpty() ? null : (decimal?)taxCell.GetValue<decimal>(),
            TotalAmount = totalAmountCell.GetValue<decimal>(),
            ProcessedFilePath = filePathCell
        };
    }

    public async Task SortReceiptsByDateAsync(int year, CancellationToken cancellationToken = default)
    {
        var excelPath = GetExcelFilePath(year);

        if (!File.Exists(excelPath))
        {
            _logger.LogWarning("Excel file does not exist, skipping sort: {Path}", excelPath);
            return;
        }

        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook(excelPath);

            foreach (var worksheet in workbook.Worksheets)
            {
                var receipts = new List<(int Row, Receipt Receipt)>();
                var row = _settings.Excel.StartRow;

                // Read all receipts from the worksheet
                while (!worksheet.Cell(row, _settings.Excel.Columns.Date).IsEmpty())
                {
                    try
                    {
                        var receipt = ParseReceiptFromRow(worksheet, row);
                        receipts.Add((row, receipt));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing row {Row} in sheet {Sheet} during sort", row, worksheet.Name);
                    }
                    row++;
                }

                if (receipts.Count == 0)
                    continue;

                // Sort by date, then by time
                var sortedReceipts = receipts
                    .OrderBy(r => r.Receipt.Date)
                    .ThenBy(r => r.Receipt.Time ?? TimeSpan.Zero)
                    .ToList();

                // Clear all data rows (keep headers)
                var lastRow = receipts.Max(r => r.Row);
                worksheet.Range(_settings.Excel.StartRow, 1, lastRow, 7).Clear();

                // Rewrite sorted data
                row = _settings.Excel.StartRow;
                foreach (var (_, receipt) in sortedReceipts)
                {
                    worksheet.Cell(row, _settings.Excel.Columns.Date).Value = receipt.Date.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, _settings.Excel.Columns.Time).Value = receipt.Time?.ToString(@"hh\:mm") ?? "";
                    worksheet.Cell(row, _settings.Excel.Columns.Restaurant).Value = receipt.Restaurant;

                    if (receipt.AmountExcludingTax.HasValue)
                        worksheet.Cell(row, _settings.Excel.Columns.AmountExcludingTax).Value = receipt.AmountExcludingTax.Value;

                    if (receipt.Tax.HasValue)
                        worksheet.Cell(row, _settings.Excel.Columns.Tax).Value = receipt.Tax.Value;

                    worksheet.Cell(row, _settings.Excel.Columns.TotalAmount).Value = receipt.TotalAmount;

                    if (!string.IsNullOrEmpty(receipt.ProcessedFilePath))
                        worksheet.Cell(row, _settings.Excel.Columns.FilePath).Value = receipt.ProcessedFilePath;

                    // Format currency columns
                    FormatCurrencyColumn(worksheet, row, _settings.Excel.Columns.AmountExcludingTax);
                    FormatCurrencyColumn(worksheet, row, _settings.Excel.Columns.Tax);
                    FormatCurrencyColumn(worksheet, row, _settings.Excel.Columns.TotalAmount);

                    row++;
                }

                _logger.LogInformation("Sorted {Count} receipts in sheet {Sheet}", sortedReceipts.Count, worksheet.Name);
            }

            workbook.Save();
            _logger.LogInformation("All receipts sorted by date in Excel file: {Path}", excelPath);
        }, cancellationToken);
    }
}
