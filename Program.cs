using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StorePitOne.Data;
using StorePitOne.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddAzureWebAppDiagnostics();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("StorePitOne", LogLevel.Information);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddScoped<UserService>();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddSingleton<CustomerService>();

builder.Services.AddScoped<PeakWmsApiClient>();
builder.Services.AddScoped<SqlService>();
builder.Services.AddScoped<StockActionExtractionService>();
builder.Services.AddScoped<PeakRawSyncService>();
builder.Services.AddScoped<StockAnalyticsService>();
builder.Services.AddScoped<PeakSyncOrchestratorService>();
builder.Services.AddScoped<BackendStatusService>();
builder.Services.AddScoped<BackendSmokeTestService>();
builder.Services.AddScoped<SystemSettingsService>();

builder.Services.AddHostedService<NightlyDatabaseUpdateService>();

builder.Services.AddHttpClient("PeakWMS", client =>
{
    client.BaseAddress = new Uri("https://api.peakwms.com/");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

Console.WriteLine("APP STARTER");

app.Run();