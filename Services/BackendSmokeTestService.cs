namespace StorePitOne.Services;

public class BackendSmokeTestService
{
    private readonly BackendStatusService _backendStatusService;
    private readonly StockAnalyticsService _stockAnalyticsService;
    private readonly ILogger<BackendSmokeTestService> _logger;

    public BackendSmokeTestService(
        BackendStatusService backendStatusService,
        StockAnalyticsService stockAnalyticsService,
        ILogger<BackendSmokeTestService> logger)
    {
        _backendStatusService = backendStatusService;
        _stockAnalyticsService = stockAnalyticsService;
        _logger = logger;
    }

    public async Task RunSmokeTest()
    {
        Console.WriteLine("SMOKE TEST STARTER");

        _logger.LogInformation("Starter backend smoke test...");

        var status = await _backendStatusService.GetStatus();

        _logger.LogInformation("Database online: {DatabaseOnline}", status.DatabaseOnline);
        _logger.LogInformation("PeakRawData rows: {PeakRawDataCount}", status.PeakRawDataCount);
        _logger.LogInformation("StockActionEvents rows: {StockActionEventsCount}", status.StockActionEventsCount);
        _logger.LogInformation("Seneste raw sync: {LatestRawSyncAt}", status.LatestRawSyncAt);
        _logger.LogInformation("Seneste event import: {LatestEventImportedAt}", status.LatestEventImportedAt);

        var deadStock = await _stockAnalyticsService.GetDeadStockItems();
        _logger.LogInformation("Dead stock rows: {DeadStockCount}", deadStock.Count);

        var slowMovers = await _stockAnalyticsService.GetSlowMoverItems();
        _logger.LogInformation("Slow mover rows: {SlowMoverCount}", slowMovers.Count);

        _logger.LogInformation("Backend smoke test færdig.");
    }
}