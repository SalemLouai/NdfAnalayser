using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NdfProcessor.Application.UseCases;
using NdfProcessor.Domain.Interfaces;
using NdfProcessor.Infrastructure.Configuration;
using NdfProcessor.Infrastructure.Services;

namespace NdfProcessor.Console;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var host = CreateHostBuilder(args).Build();

            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("NdfProcessor starting...");

            // Execute processing
            var useCase = services.GetRequiredService<ProcessReceiptsUseCase>();
            var summary = await useCase.ExecuteAsync();

            // Display summary
            System.Console.WriteLine();
            System.Console.WriteLine($"=== Run {summary.RunId} completed ===");
            System.Console.WriteLine($"Receipts processed: {summary.ReceiptsProcessed}");
            System.Console.WriteLine($"Receipts in error: {summary.ErrorCount}");
            System.Console.WriteLine($"Duplicates detected: {summary.DuplicateCount}");
            System.Console.WriteLine($"Excel file updated: {summary.ExcelFilePath}");
            System.Console.WriteLine($"Duration: {summary.Duration}");
            System.Console.WriteLine();

            logger.LogInformation("NdfProcessor completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"Fatal error: {ex.Message}");
            System.Console.ResetColor();
            System.Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                    optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<AppSettings>(context.Configuration);

                // Domain services
                services.AddTransient<IOcrService, AzureOcrService>();
                services.AddTransient<IExcelService, ClosedXmlExcelService>();
                services.AddTransient<IFileService, LocalFileService>();
                services.AddTransient<IPdfService, PdfService>();
                services.AddTransient<IDuplicateDetector, DuplicateDetector>();

                // Application services
                services.AddTransient<ProcessReceiptsUseCase>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Information);
            });
}
