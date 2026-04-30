# NdfProcessor - Setup and Usage Guide

## Table of Contents
1. [Prerequisites](#prerequisites)
2. [Azure Setup](#azure-setup)
3. [Local Configuration](#local-configuration)
4. [Building the Application](#building-the-application)
5. [Configuration](#configuration)
6. [Running the Application](#running-the-application)
7. [Folder Structure](#folder-structure)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Software
- **.NET 10 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Azure CLI** - [Download here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- **Azure Subscription** with access to create Cognitive Services resources
- **Microsoft Excel** (for viewing/editing the output files)
- **OneDrive** (recommended for folder synchronization)

### System Requirements
- **Operating System**: Windows 10/11, macOS, or Linux
- **RAM**: 4GB minimum, 8GB recommended
- **Disk Space**: 500MB for application + space for receipts and processed files

---

## Azure Setup

### Step 1: Login to Azure

```bash
az login
```

### Step 2: Create Resource Group

```bash
az group create \
  --name ndf-processor-rg \
  --location eastus
```

### Step 3: Create Document Intelligence Resource

```bash
az cognitiveservices account create \
  --name ndf-document-intelligence \
  --resource-group ndf-processor-rg \
  --kind FormRecognizer \
  --sku S0 \
  --location eastus \
  --yes
```

### Step 4: Get Endpoint and API Key

```bash
# Get Endpoint
az cognitiveservices account show \
  --name ndf-document-intelligence \
  --resource-group ndf-processor-rg \
  --query properties.endpoint

# Get API Key
az cognitiveservices account keys list \
  --name ndf-document-intelligence \
  --resource-group ndf-processor-rg \
  --query key1
```

**Save these values** - you'll need them for configuration.

---

## Local Configuration

### Step 1: Clone/Download the Project

```bash
cd C:\chatgbt\NdfAnalyser
```

### Step 2: Create Folder Structure

Create the following folders on your local machine (or OneDrive):

```
C:\Users\<YourName>\OneDrive\ExpenseReports\
├── ToProcess\          # Put your receipt images/PDFs here
├── Processed\          # Processed receipts (organized by month)
├── Errors\             # Files that failed processing
└── Reports\            # Error reports
```

### Step 3: Prepare Excel Template

Create an Excel file:
- **Location**: `C:\Users\<YourName>\OneDrive\Accounting\2024.xlsx`
- **Structure**: The application will create sheets named `01`, `02`, ..., `12` (one per month)
- **Headers** (Row 1):
  - Column A: Date
  - Column B: Time
  - Column C: Restaurant
  - Column D: Amount Excl. Tax
  - Column E: Tax
  - Column F: Total Amount

Or let the application create it automatically on first run.

### Step 4: Configure Application

Edit `src/NdfProcessor.Console/appsettings.json`:

```json
{
  "AzureOcr": {
    "Endpoint": "https://ndf-document-intelligence.cognitiveservices.azure.com/",
    "ApiKey": "<YOUR_API_KEY_HERE>",
    "ModelId": "prebuilt-receipt"
  },
  "Paths": {
    "InputFolder": "C:\\Users\\<YourName>\\OneDrive\\ExpenseReports\\ToProcess",
    "ErrorFolder": "C:\\Users\\<YourName>\\OneDrive\\ExpenseReports\\Errors",
    "ProcessedFolderRoot": "C:\\Users\\<YourName>\\OneDrive\\ExpenseReports\\Processed",
    "ExcelFilePath": "C:\\Users\\<YourName>\\OneDrive\\Accounting\\{year}.xlsx",
    "ErrorReportFolder": "C:\\Users\\<YourName>\\OneDrive\\ExpenseReports\\Reports"
  }
}
```

**Replace**:
- `<YOUR_API_KEY_HERE>` with your Azure API key
- `<YourName>` with your Windows username
- Adjust paths as needed for your setup

---

## Building the Application

### Build Solution

```bash
dotnet build
```

### Run Tests (Optional)

```bash
dotnet test
```

### Publish for Deployment (Optional)

```bash
dotnet publish src/NdfProcessor.Console/NdfProcessor.Console.csproj \
  -c Release \
  -o publish
```

---

## Running the Application

### Option 1: Run from Source

```bash
dotnet run --project src/NdfProcessor.Console/NdfProcessor.Console.csproj
```

### Option 2: Run Published Application

```bash
cd publish
./NdfProcessor.Console.exe
```

### Option 3: Schedule Automatic Processing

**Windows Task Scheduler**:
1. Open Task Scheduler
2. Create Basic Task
3. Trigger: Daily or when folder changes
4. Action: Start a program
5. Program: `C:\chatgbt\NdfAnalyser\publish\NdfProcessor.Console.exe`

---

## Configuration

### Supported File Formats

**Images**:
- `.jpg` / `.jpeg`
- `.png`

**Documents**:
- `.pdf` (including multi-receipt PDFs)

### Excel Configuration

**Sheet Naming**: Sheets are named `01` to `12` (January to December)

**Starting Row**: Data starts at row 2 (row 1 is headers)

**Columns** (configurable in appsettings.json):
- **A**: Date (MM/dd/yyyy format)
- **B**: Time (HH:mm format)
- **C**: Restaurant name
- **D**: Amount Excluding Tax
- **E**: Tax
- **F**: Total Amount

### File Naming After Processing

Processed files are renamed as:
```
yyyy-MM-dd_<amount>_USD.ext
```

Example: `2024-01-15_25_50_USD.jpg`

If duplicate names exist, a suffix is added: `_1`, `_2`, etc.

### Folder Organization

Processed files are organized by month:
```
Processed\
├── 2024-01\
│   ├── 2024-01-15_25_50_USD.jpg
│   └── 2024-01-20_18_75_USD.pdf
└── 2024-02\
    └── 2024-02-05_32_00_USD.jpg
```

---

## How It Works

### Processing Pipeline

1. **Scan Input Folder**: Finds all JPG, PNG, and PDF files
2. **OCR Analysis**: Sends each file to Azure Document Intelligence
3. **Multi-Receipt Detection**: For PDFs with multiple receipts:
   - Rasterizes each page
   - Detects individual receipts via bounding boxes
   - Crops each receipt into separate images
4. **Duplicate Detection**: Checks for existing receipts (Date + Time + Amount + Restaurant)
5. **Excel Insertion**: Adds receipt data to the appropriate month's sheet
6. **File Organization**: Moves processed files to organized folders
7. **Error Handling**: Moves failed files to error folder and generates report

### Duplicate Detection

A receipt is considered a duplicate if it matches an existing entry on **all four criteria**:
- Date (yyyy-MM-dd)
- Time (HH:mm)
- Restaurant name (case-insensitive)
- Total amount (exact match)

Duplicates are **not inserted** into Excel and a warning is logged.

### Error Handling

Files that fail processing:
- Moved to `Errors\` folder
- Listed in error report: `Reports\ErrorReport_<RunId>.txt`
- Common errors:
  - Unreadable/corrupt images
  - No receipts detected
  - OCR confidence too low
  - Azure API errors

---

## Troubleshooting

### Issue: "Excel file is locked"

**Solution**: Close the Excel file before running the processor. The application needs exclusive access.

### Issue: "No receipts detected"

**Causes**:
- Image quality too low (use 300 DPI minimum)
- Receipt not clearly visible
- Non-receipt document

**Solution**: Use higher quality images or PDFs with clear, legible text.

### Issue: "Azure API rate limit"

**Error**: HTTP 429

**Solution**: The application automatically retries with exponential backoff. If persistent, upgrade Azure tier or reduce batch size.

### Issue: "File not found" errors

**Solution**: Verify all paths in `appsettings.json` exist and are accessible.

### Issue: Wrong date format in Excel

**Solution**: The application uses US date format (MM/dd/yyyy). Adjust Excel regional settings or modify the code if needed.

---

## Testing

### Test with Sample Receipts

1. Place a few receipt images in `ToProcess\` folder
2. Run the application
3. Check:
   - Excel file has new entries
   - Files moved to `Processed\yyyy-MM\`
   - No files in `Errors\` folder

### Verify Duplicate Detection

1. Process a receipt
2. Place the same receipt image again
3. Run application
4. Verify: warning logged, no duplicate entry in Excel

---

## Cost Estimation

### Azure Document Intelligence

**S0 Tier Pricing** (as of 2024):
- First 500 pages: $1.50 per 1,000 pages
- Next 9,500 pages: $1.00 per 1,000 pages

**Example**:
- 100 receipts/month = ~$0.15/month
- 500 receipts/month = ~$0.75/month

### Tips to Reduce Costs

- Use Free Tier (F0) for development/testing
- Process receipts in batches (weekly vs. daily)
- Delete test images from input folder

---

## Advanced Usage

### Command Line Arguments

```bash
# Custom configuration file
dotnet run --project src/NdfProcessor.Console -- \
  --Configuration:AzureOcr:ApiKey="<key>"

# Set log level
dotnet run --project src/NdfProcessor.Console -- \
  --Logging:LogLevel:Default=Debug
```

### Environment Variables

Set via environment:
```bash
export AzureOcr__ApiKey="<your-key>"
export Paths__InputFolder="/path/to/receipts"
```

Windows:
```cmd
set AzureOcr__ApiKey=<your-key>
```

---

## Support

### Logs

Check console output for detailed processing information:
- Information: Normal operations
- Warning: Duplicates, incomplete data
- Error: Processing failures

### Error Reports

Error reports are saved in `Reports\ErrorReport_<RunId>.txt` with details about failed files.

---

## Next Steps

1. ✅ Complete Azure setup
2. ✅ Configure local folders
3. ✅ Update appsettings.json
4. ✅ Test with sample receipts
5. ✅ Set up scheduled processing (optional)
6. ✅ Monitor and optimize

**You're ready to start processing receipts automatically!**
