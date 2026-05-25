using Microsoft.Extensions.DependencyInjection;

namespace StorePitOne.Services;

public class NightlyDatabaseUpdateService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public NightlyDatabaseUpdateService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("NightlyDatabaseUpdateService er startet.");
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var settingsService =
                    scope.ServiceProvider.GetRequiredService<SystemSettingsService>();

                var orchestrator =
                    scope.ServiceProvider.GetRequiredService<PeakSyncOrchestratorService>();

                var settings = await settingsService.GetSettings();

                var now = DateTime.Now;

                var alreadyRanToday =
                    settings.LastNightlyRunAt?.Date == now.Date;

                var currentTime = new TimeSpan(now.Hour, now.Minute, 0);

                var scheduledTime = new TimeSpan(
                    settings.NightlyUpdateTime.Hours,
                    settings.NightlyUpdateTime.Minutes,
                    0);

                var withinWindow =
                    currentTime >= scheduledTime &&
                    currentTime < scheduledTime.Add(TimeSpan.FromMinutes(14));

                Console.WriteLine("----- Nightly check -----");
                Console.WriteLine($"Nu: {now:dd-MM-yyyy HH:mm:ss}");
                Console.WriteLine($"Aktiv: {settings.NightlyUpdateEnabled}");
                Console.WriteLine($"Planlagt tid: {scheduledTime}");
                Console.WriteLine($"Sidste kørsel: {settings.LastNightlyRunAt}");
                Console.WriteLine($"Allerede kørt i dag: {alreadyRanToday}");
                Console.WriteLine($"Indenfor tidsvindue: {withinWindow}");

                if (
                    settings.NightlyUpdateEnabled &&
                    withinWindow &&
                    !alreadyRanToday
                )
                {
                    Console.WriteLine("NATLIG UPDATE STARTER NU");

                    await orchestrator.UpdateWholeDatabase();

                    await settingsService.MarkNightlyRunCompleted();

                    Console.WriteLine("Natlig databaseopdatering færdig.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fejl i NightlyDatabaseUpdateService:");
                Console.WriteLine(ex.Message);
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}