# NDF Processor - Automated Expense Report System

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Azure](https://img.shields.io/badge/Azure-Document%20Intelligence-0078D4)](https://azure.microsoft.com/)
[![Clean Architecture](https://img.shields.io/badge/Architecture-Clean-blue)](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

An intelligent, automated expense report processing system for restaurant receipts. Built with .NET 10 and Azure Document Intelligence, following Clean Architecture principles.

## 🎯 Overview

NdfProcessor automates the complete workflow for processing restaurant expense receipts:
- 📸 Scans photos and PDFs from OneDrive-synced folders
- 🤖 Extracts receipt data using Azure Document Intelligence OCR
- 📄 Handles multi-receipt PDFs with intelligent cropping
- 📊 Automatically updates Excel spreadsheets
- 🗂️ Organizes processed files by month
- ✅ Detects and prevents duplicates

## ✨ Features

- **Multi-format support**: JPG, PNG, and PDF files
- **Multi-receipt detection**: Automatically splits PDFs with multiple receipts
- **Duplicate prevention**: Smart detection based on date, time, amount, and merchant
- **Excel integration**: Direct insertion into accountant-provided templates
- **Error handling**: Comprehensive retry logic and error reporting
- **Clean Architecture**: Maintainable, testable, and scalable design

## 📋 Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- Azure subscription with Document Intelligence access
- Microsoft Excel (for viewing/editing output files)
- OneDrive (recommended for folder synchronization)

## 🚀 Quick Setup Guide

### Step 1: Azure Setup

Login and create Azure Document Intelligence resource:

```bash
# Login to Azure
az login

# Create resource group
az group create --name ndf-processor-rg --location eastus

# Create Document Intelligence resource
az cognitiveservices account create \
  --name ndf-document-intelligence \
  --resource-group ndf-processor-rg \
  --kind FormRecognizer \
  --sku S0 \
  --location eastus \
  --yes

# Get your endpoint and API key
az cognitiveservices account show \
  --name ndf-document-intelligence \
  --resource-group ndf-processor-rg \
  --query properties.endpoint

az cognitiveservices account keys list \
  --name ndf-document-intelligence \
  --resource-group ndf-processor-rg \
  --query key1
```

**Save the endpoint and API key** - you'll need them in the next step.

### Step 2: Create Folder Structure

Create these folders on your local machine (or OneDrive):

```
C:\Users\<YourName>\OneDrive\ExpenseReports\
├── ToProcess\          # Put your receipt images/PDFs here
├── Processed\          # Processed receipts (organized by month)
├── Errors\             # Files that failed processing
└── Reports\            # Error reports
```

### Step 3: Configure the Application

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

Replace `<YOUR_API_KEY_HERE>` and `<YourName>` with your actual values.

### Step 4: Build and Run

```bash
# Build the solution
dotnet build

# Run tests (optional)
dotnet test

# Run the application
dotnet run --project src/NdfProcessor.Console
```

### Step 5: Process Your First Receipt

1. Place a receipt image (JPG, PNG) or PDF in the `ToProcess` folder
2. Run the application
3. Check the Excel file for the new entry
4. Verify the file was moved to `Processed\yyyy-MM\`

📖 **For detailed setup instructions, troubleshooting, and advanced usage**, see [SETUP.md](./SETUP.md)

## 📁 Project Structure

```
NdfAnalyser/
├── src/
│   ├── NdfProcessor.Domain/          # Business entities and interfaces
│   ├── NdfProcessor.Application/     # Use cases and orchestration
│   ├── NdfProcessor.Infrastructure/  # External services (Azure, Excel, File I/O)
│   └── NdfProcessor.Console/         # Application entry point
├── tests/
│   ├── NdfProcessor.Domain.Tests/
│   ├── NdfProcessor.Application.Tests/
│   └── NdfProcessor.Infrastructure.Tests/
├── infrastructure/
│   ├── bootstrap.sh                  # Azure setup script
│   └── terraform/                    # Infrastructure as Code
└── CLAUDE.md                         # Complete system specification
```

## 🧪 Testing

Run all tests:
```bash
dotnet test
```

Run with coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 📝 Configuration

See `appsettings.json` for full configuration options:
- Azure Document Intelligence settings
- Input/output folder paths
- Excel template configuration
- Processing options

## 🏗️ Architecture

This project follows **Clean Architecture** principles:

- **Domain Layer**: Core business logic, zero external dependencies
- **Application Layer**: Use cases and orchestration
- **Infrastructure Layer**: External integrations (Azure, Excel, File System)
- **Presentation Layer**: Console application entry point

## 📖 Documentation

- [CLAUDE.md](./CLAUDE.md) - Complete system specification
- [Rules](./.claude/rules/) - Coding standards and architectural guidelines

## 🤝 Contributing

This project follows strict coding standards:
- All code and comments in English
- Clean Architecture boundaries enforced
- Async/await for all I/O operations
- Dependency injection throughout
- Comprehensive logging with `ILogger<T>`

See [coding rules](./.claude/rules/) for details.

## 📄 License

[Add your license here]

## 👥 Authors

[Add author information here]
