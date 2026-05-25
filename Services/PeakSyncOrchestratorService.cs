namespace StorePitOne.Services;

public class PeakSyncOrchestratorService
{
    private readonly SqlService _sqlService;
    private readonly PeakWmsApiClient _peakClient;
    private readonly PeakRawSyncService _rawSyncService;
    private readonly StockActionExtractionService _extractionService;
    private readonly ILogger<PeakSyncOrchestratorService> _logger;

    public PeakSyncOrchestratorService(
        SqlService sqlService,
        PeakWmsApiClient peakClient,
        PeakRawSyncService rawSyncService,
        StockActionExtractionService extractionService,
        ILogger<PeakSyncOrchestratorService> logger)
    {
        _sqlService = sqlService;
        _peakClient = peakClient;
        _rawSyncService = rawSyncService;
        _extractionService = extractionService;
        _logger = logger;
    }

    public async Task UpdateWholeDatabase()
    {
        _logger.LogInformation("Starter opdatering af hele databasen...");

        await SyncProductsAllCustomers();

        await SyncStockAllCustomers();

        await SyncStockActionRawDataAllCustomers();

        _logger.LogInformation("Starter extraction af stock actions...");
        await _extractionService.ExtractStockActionsFromRawData();
        _logger.LogInformation("Extraction af stock actions færdig.");

        _logger.LogInformation("Hele databasen er opdateret.");
    }

    public async Task SyncStockActionRawDataAllCustomers()
    {
        _logger.LogInformation("Starter raw sync af stock actions...");

        var customers = await _sqlService.GetCustomers();

        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        _logger.LogInformation(
            "Raw stockAction sync periode: {Start} til {End}",
            start,
            end);

        foreach (var customer in customers)
        {
            _logger.LogInformation(
                "Starter raw stockAction sync for CustomerId {CustomerId}",
                customer.Id);

            var pages = await _peakClient.GetStockActionRawPagesAsync(
                customer.ApiKey,
                start,
                end
            );

            foreach (var page in pages)
            {
                await _rawSyncService.InsertPeakRawData(
                    customer.Id,
                    "stockAction",
                    page.Url,
                    page.StatusCode,
                    page.TotalRecords,
                    page.RawJson
                );
            }

            _logger.LogInformation(
                "CustomerId {CustomerId}: {PageCount} raw stockAction sider gemt",
                customer.Id,
                pages.Count);
        }

        _logger.LogInformation("Raw stockAction sync færdig.");
    }

    public async Task BackfillAllCustomers()
    {
        _logger.LogInformation("Starter backfill for alle kunder...");

        await SyncStockActionRawDataAllCustomers();

        _logger.LogInformation("Starter extraction efter backfill...");
        await _extractionService.ExtractStockActionsFromRawData();
        _logger.LogInformation("Extraction efter backfill færdig.");

        _logger.LogInformation("Backfill for alle kunder færdig.");
    }

    public async Task SyncProductsAllCustomers()
    {
        _logger.LogInformation("Starter product sync...");

        var customers = await _sqlService.GetCustomers();

        foreach (var customer in customers)
        {
            var products = await _peakClient.GetProductsAsync(customer.ApiKey);

            _logger.LogInformation(
                "CustomerId {CustomerId}: {ProductCount} products hentet",
                customer.Id,
                products.Count);

            if (products.Any())
            {
                await _sqlService.InsertProducts(customer.Id, products);

                _logger.LogInformation(
                    "CustomerId {CustomerId}: products gemt i databasen",
                    customer.Id);
            }
        }

        _logger.LogInformation("Product sync færdig.");
    }

    public async Task SyncStockAllCustomers()
    {
        _logger.LogInformation("Starter stock sync...");

        var customers = await _sqlService.GetCustomers();

        foreach (var customer in customers)
        {
            var stock = await _peakClient.GetStockAsync(customer.ApiKey);

            _logger.LogInformation(
                "CustomerId {CustomerId}: {StockCount} stock rows hentet",
                customer.Id,
                stock.Count);

            if (stock.Any())
            {
                await _sqlService.InsertStock(customer.Id, stock);

                _logger.LogInformation(
                    "CustomerId {CustomerId}: stock gemt i databasen",
                    customer.Id);
            }
        }

        _logger.LogInformation("Stock sync færdig.");
    }
}