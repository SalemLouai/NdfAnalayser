namespace NdfProcessor.Infrastructure.Configuration;

/// <summary>
/// Application settings mapped from appsettings.json
/// </summary>
public class AppSettings
{
    public AzureOcrSettings AzureOcr { get; set; } = new();
    public PathsSettings Paths { get; set; } = new();
    public ExcelSettings Excel { get; set; } = new();
    public ProcessingSettings Processing { get; set; } = new();
}

public class AzureOcrSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "prebuilt-receipt";
}

public class PathsSettings
{
    public string InputFolder { get; set; } = string.Empty;
    public string ErrorFolder { get; set; } = string.Empty;
    public string ProcessedFolderRoot { get; set; } = string.Empty;
    public string ExcelFilePath { get; set; } = string.Empty;
    public string ErrorReportFolder { get; set; } = string.Empty;
}

public class ExcelSettings
{
    public string SheetNameFormat { get; set; } = "{month:D2}";
    public int StartRow { get; set; } = 2;
    public ColumnSettings Columns { get; set; } = new();
}

public class ColumnSettings
{
    public string Date { get; set; } = "A";
    public string Time { get; set; } = "B";
    public string Restaurant { get; set; } = "C";
    public string AmountExcludingTax { get; set; } = "D";
    public string Tax { get; set; } = "E";
    public string TotalAmount { get; set; } = "F";
    public string FilePath { get; set; } = "G";
}

public class ProcessingSettings
{
    public List<string> SupportedImageExtensions { get; set; } = new() { ".jpg", ".jpeg", ".png" };
    public List<string> SupportedPdfExtensions { get; set; } = new() { ".pdf" };
    public string ProcessedFileNameFormat { get; set; } = "{date:yyyy-MM-dd}_{totalAmount}_USD";
    public string ProcessedSubFolderFormat { get; set; } = "yyyy-MM";
}
