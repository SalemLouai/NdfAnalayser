# Automated Expense Report Processing System

> **Instructions**: This document contains the complete specifications for the NdfProcessor system. All code, comments, documentation, and user-facing messages must be in English.

---

You are a senior software architect specialized in .NET and Azure. You must design and code a complete automated restaurant expense report processing system. The system is a **.NET 10 Console App** using **Clean Architecture** that:

1. Scans a local folder (OneDrive synced) containing photos (JPG/PNG) and PDFs of restaurant receipts
2. Sends each image/page to **Azure Document Intelligence** (`prebuilt-receipt`) for OCR extraction
3. Handles **multi-receipt PDFs**: detects each receipt individually via bounding boxes returned by the API, rasterizes the page, and crops each receipt into individual images
4. Extracts from each receipt: **date, time, restaurant name, amount excluding tax, tax, total amount including tax**
5. Inserts data into an **existing Excel file** (provided by accountant) via **ClosedXML**
6. Renames and moves processed files to a subfolder organized by month
7. Handles errors, duplicates, and produces a run summary

---

## 1. Project Architecture — Clean Architecture

```
src/
├── NdfProcessor.Domain/            # Entities, interfaces, value objects
│   ├── Entities/
│   │   ├── Receipt.cs              # Date, Time, Restaurant, AmountExcludingTax, Tax, TotalAmount
│   │   ├── ProcessingResult.cs     # Processing result for a file
│   │   └── RunSummary.cs           # Run summary (processed, errors, duplicates, RunId)
│   ├── Interfaces/
│   │   ├── IOcrService.cs          # OCR extraction
│   │   ├── IExcelService.cs        # Excel read/write
│   │   ├── IFileService.cs         # File operations (read, move, rename)
│   │   ├── IPdfService.cs          # PDF rasterization, crop by bounding box
│   │   └── IDuplicateDetector.cs   # Duplicate detection
│   └── Enums/
│       └── ProcessingStatus.cs     # Success, Error, Duplicate
│
├── NdfProcessor.Infrastructure/     # Concrete implementations
│   ├── Services/
│   │   ├── AzureOcrService.cs      # Azure Document Intelligence REST API call
│   │   ├── ClosedXmlExcelService.cs # Excel manipulation with ClosedXML
│   │   ├── LocalFileService.cs     # Local file system management
│   │   ├── PdfService.cs           # Rasterization with SkiaSharp + bounding box crop
│   │   └── DuplicateDetector.cs    # Duplicate detection (date+time+amount+restaurant)
│   └── Configuration/
│       └── AppSettings.cs          # Strongly-typed appsettings.json mapping
│
├── NdfProcessor.Application/        # Use cases / orchestration
│   ├── UseCases/
│   │   └── ProcessReceiptsUseCase.cs # Main pipeline orchestration
│   └── DTOs/
│       └── OcrResult.cs            # OCR return DTO
│
├── NdfProcessor.Console/            # Entry point
│   ├── Program.cs                  # DI setup, configuration, launch
│   └── appsettings.json            # Complete configuration
│
tests/
├── NdfProcessor.Domain.Tests/
├── NdfProcessor.Infrastructure.Tests/
└── NdfProcessor.Application.Tests/
```

Each project has its own NuGet dependencies. Inter-project references follow Clean Architecture rules:
- **Domain**: no external dependencies
- **Application**: references Domain only
- **Infrastructure**: references Domain and Application
- **Console**: references all projects

---

## 2. Configuration — `appsettings.json`

All configuration is externalized and **100% configurable**:

```json
{
  "AzureOcr": {
    "Endpoint": "https://<resource-name>.cognitiveservices.azure.com/",
    "ApiKey": "<api-key>",
    "ModelId": "prebuilt-receipt"
  },
  "Paths": {
    "InputFolder": "C:\\Users\\<user>\\OneDrive\\ExpenseReports\\ToProcess",
    "ErrorFolder": "C:\\Users\\<user>\\OneDrive\\ExpenseReports\\Errors",
    "ProcessedFolderRoot": "C:\\Users\\<user>\\OneDrive\\ExpenseReports\\Processed",
    "ExcelFilePath": "C:\\Users\\<user>\\OneDrive\\Accounting\\{year}.xlsx",
    "ErrorReportFolder": "C:\\Users\\<user>\\OneDrive\\ExpenseReports\\Reports"
  },
  "Excel": {
    "SheetNameFormat": "{month:D2}",
    "StartRow": 2,
    "Columns": {
      "Date": "A",
      "Time": "B",
      "Restaurant": "C",
      "AmountExcludingTax": "D",
      "Tax": "E",
      "TotalAmount": "F"
    }
  },
  "Processing": {
    "SupportedImageExtensions": [ ".jpg", ".jpeg", ".png" ],
    "SupportedPdfExtensions": [ ".pdf" ],
    "ProcessedFileNameFormat": "{date:yyyy-MM-dd}_{totalAmount}_USD",
    "ProcessedSubFolderFormat": "yyyy-MM"
  }
}
```

**Notes:**
- `{year}` in `ExcelFilePath` is dynamically replaced by the receipt year
- `SheetNameFormat`: tabs are named `01`, `02`... `12`
- `StartRow`: data start row (headers are above)
- Columns are mapped by letter for total flexibility
- Excel file name is dynamic (one file per year)

---

## 3. Processing Pipeline — `ProcessReceiptsUseCase`

Main workflow:

```
1. Generate unique RunId (short GUID or timestamp)
2. Scan input folder for all JPG/PNG/PDF files
3. For each file:
   a. If IMAGE (jpg/png):
      - Send to Azure Document Intelligence (prebuilt-receipt)
      - API can detect multiple receipts on same image
      - For each detected receipt: extract fields
   b. If PDF:
      - For each PDF page:
        - Send page to Azure Document Intelligence
        - API detects N receipts with their bounding boxes
        - Rasterize page as high-resolution image (300 DPI min)
        - For each detected receipt: crop via bounding box → individual image
        - Extract fields from each receipt
   c. For each extracted Receipt:
      - Check for duplicates (date + time + amount + restaurant name)
        → If duplicate: WARNING in console with filename, skip
      - Insert into correct Excel tab (month corresponding to receipt date)
      - Add row at first empty line after StartRow
   d. Move source file:
      - If SUCCESS: to ProcessedFolderRoot/yyyy-MM/yyyy-MM-dd_amount_USD.ext
        (if multi-receipt PDF, move individually cropped images)
      - If OCR ERROR: to ErrorFolder + log
4. Generate error report file (if errors present):
   - Name: ErrorReport_{RunId}.txt
   - Content: RunId, run date/time, list of error files with reason
5. Display summary in console:
   === Run {RunId} completed ===
   Receipts processed: X
   Receipts in error: Y
   Duplicates detected: Z
   Excel file updated: {path}
```

---

## 4. Azure Document Intelligence — `AzureOcrService`

- Use REST API v4.0 or SDK `Azure.AI.FormRecognizer` (latest stable version compatible with .NET 10)
- Model: `prebuilt-receipt`
- Fields to extract from receipt:
  - `MerchantName` → Restaurant name
  - `TransactionDate` → Date
  - `TransactionTime` → Time
  - `Subtotal` → Amount excluding tax
  - `TotalTax` → Tax
  - `Total` → Total amount including tax
- If `Subtotal` is missing: calculate `AmountExcludingTax = Total - TotalTax`
- If `TotalTax` is missing: calculate `Tax = Total - Subtotal`
- If only `Total` is available: put `Total` in TotalAmount, others to `null` → warning in log
- Implement retry (3 attempts with exponential backoff) for transient API errors
- Each receipt returned by API has `BoundingRegions` field with coordinates → use for cropping

---

## 5. Multi-Receipt PDF Handling — `PdfService`

- Use **SkiaSharp** for PDF page rasterization to images
- Use **PdfPig** to read PDF and extract pages
- PDF workflow:
  1. Read PDF with PdfPig
  2. For each page: rasterize to PNG image at 300 DPI with SkiaSharp
  3. Send full page image to OCR
  4. For each receipt detected in OCR response:
     - Retrieve `BoundingRegions` (polygon)
     - Calculate bounding rectangle
     - Crop page image to this rectangle (with 10px margin)
     - Save crop as individual image
  5. Each cropped image is treated as individual receipt

---

## 6. Duplicate Detection — `DuplicateDetector`

- Before inserting row into Excel, check if identical entry already exists
- Comparison criteria: **Date + Time + Total Amount + Restaurant Name** (case-insensitive, trimmed)
- If duplicate detected:
  - Display **WARNING** in console: `⚠️ DUPLICATE DETECTED: {fileName} — {date} {restaurant} ${totalAmount}`
  - Do not insert into Excel
  - Count in summary
- Load existing Excel data at run start to build comparison cache

---

## 7. File Management — `LocalFileService`

- **Successfully processed file**:
  - Create `yyyy-MM` subfolder under `ProcessedFolderRoot` if not exists
  - Rename file: `{date:yyyy-MM-dd}_{totalAmount}_USD.{extension}`
  - If name exists (two receipts same day same amount), add suffix `_1`, `_2`, etc.
  - Move to subfolder
- **Error file**:
  - Move to `ErrorFolder`
  - Keep original name
- **Supported formats**: `.jpg`, `.jpeg`, `.png`, `.pdf` (configurable)

---

## 8. Excel — `ClosedXmlExcelService`

- Use **ClosedXML** (NuGet `ClosedXML`)
- Open existing Excel file (do not recreate)
- Determine target tab from receipt date month (tabs named `01` to `12`)
- If Excel file for year doesn't exist → create with 12 tabs and headers
- Find first empty row starting from `StartRow`
- Write values in configured columns
- Format amounts as numbers with 2 decimals
- Format date according to US locale (`MM/dd/yyyy`)
- **Save file after each receipt** to prevent data loss on crash
- Handle case where Excel file is open by another program (retry or clear message)

---

## 9. Unit Tests — xUnit + NSubstitute

Create tests for:

### Domain Tests
- `Receipt`: field validation, missing calculations for ExcludingTax/Tax/Total

### Application Tests
- `ProcessReceiptsUseCase`: mock all services, test complete flow
  - Nominal case: 3 files → 3 Excel insertions
  - OCR error case: 1 unreadable file → moved to error
  - Duplicate case: 2 identical files → 1 insertion + 1 warning
  - Multi-receipt PDF case: 1 PDF with 3 receipts → 3 insertions
  - Mixed case: combine all above cases

### Infrastructure Tests
- `DuplicateDetector`: test comparison (case-insensitive, trim, exact matching)
- `ClosedXmlExcelService`: test insertion in real temporary Excel file
- `LocalFileService`: test renaming, suffix handling, folder creation

Use `NSubstitute` for mocks. Each test is isolated and doesn't depend on real file system (except for Excel and FileService integration tests using temporary folder cleaned after each test).

---

## 10. Terraform — Azure Document Intelligence Provisioning

### Bootstrap Script (`infrastructure/bootstrap.sh`)

```bash
#!/bin/bash
# Bootstrap: creates Resource Group + Storage Account for Terraform state
# Configurable variables
```

Script must:
1. Accept **configurable variables**: RG name, storage account name, region, container name
2. Default region: `eastus`
3. Check if RG already exists, **create only if not exists**
4. Create Storage Account for Terraform state
5. Create blob container for state
6. Display values to report in `backend.tf`

### Terraform (`infrastructure/terraform/`)

Files to generate:

- **`variables.tf`**: all configurable variables (resource name, RG, region, SKU, tags)
- **`main.tf`**: Resource Group (data or resource depending on existence) + Azure Cognitive Services Account with kind `FormRecognizer` and SKU `S0`, model `prebuilt-receipt`
- **`backend.tf`**: Azure Storage backend (values from bootstrap)
- **`outputs.tf`**: endpoint and API key as outputs (to report in `appsettings.json`)
- **`terraform.tfvars.example`**: example variables

---

## 11. README.md

Generate complete README.md with:

1. **Project description**: what the system does, target audience
2. **Prerequisites**: .NET 10 SDK, Azure CLI, Terraform, Azure account with VS Enterprise subscription
3. **Installation**:
   - Clone repo
   - Provision Azure infrastructure (bootstrap + terraform apply)
   - Report endpoint/key in `appsettings.json`
   - Configure local OneDrive paths
4. **Configuration**: detail of each `appsettings.json` section
5. **Usage**: how to launch app, console output examples
6. **Architecture**: Clean Architecture diagram, each layer responsibilities
7. **Special cases handling**: multi-receipt PDFs, duplicates, errors, renamed files
8. **Tests**: how to run tests (`dotnet test`)
9. **Project structure**: file tree

---

## 12. Non-Negotiable Technical Constraints

- **No raw `Console.WriteLine`**: use `ILogger<T>` from `Microsoft.Extensions.Logging` with appropriate levels (Information, Warning, Error)
- **Dependency injection** via `Microsoft.Extensions.DependencyInjection`
- **Configuration** via `Microsoft.Extensions.Configuration` + binding to POCO classes
- **Async/await** everywhere applicable
- **Nullable reference types** enabled
- **No magic strings**: constants or configuration
- NuGet packages must be in versions **compatible with .NET 10**
- Each `.cs` file must have necessary `using` statements
- Code must compile without errors
- **All code, comments, documentation in ENGLISH**

---

## 13. Expected Deliverables

Generate **ALL** following files, complete and functional:

1. `.csproj` files for each project (with NuGet packages)
2. `NdfProcessor.sln` solution file
3. All C# files for each layer (Domain, Application, Infrastructure, Console)
4. `appsettings.json` with example values
5. All test files
6. `infrastructure/bootstrap.sh`
7. All Terraform files (`main.tf`, `variables.tf`, `backend.tf`, `outputs.tf`, `terraform.tfvars.example`)
8. `README.md`
9. `.gitignore` adapted for .NET + Terraform

**Generate complete code for each file. No shortcuts, no `// TODO`, no `...`. Each file must be complete and ready to copy into an IDE.**

---

## Additional US Market Requirements

- Use **USD** currency symbol in file naming and logging
- Use **US date format** (`MM/dd/yyyy`) in Excel and user-facing messages
- All user-facing messages in **American English**
- Configuration paths use US-style folder naming conventions
- Error messages and logs use standard American English terminology
