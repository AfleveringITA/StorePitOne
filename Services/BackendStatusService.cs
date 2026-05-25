using Microsoft.Data.SqlClient;

namespace StorePitOne.Services;

public class BackendStatusService
{
    private readonly string _connectionString;

    public BackendStatusService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("StorePitDb")!;
    }

    public class BackendStatus
    {
        public bool DatabaseOnline { get; set; }

        public int PeakRawDataCount { get; set; }

        public int StockActionEventsCount { get; set; }

        public DateTime? LatestRawSyncAt { get; set; }

        public DateTime? LatestEventImportedAt { get; set; }
    }

    public async Task<BackendStatus> GetStatus()
    {
        var status = new BackendStatus();

        try
        {
            using var conn = new SqlConnection(_connectionString);

            await conn.OpenAsync();

            status.DatabaseOnline = true;

            var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
    (SELECT COUNT(*) FROM PeakRawData),
    (SELECT COUNT(*) FROM StockActionEvents),
    (SELECT DATEADD(HOUR, 2, MAX(SyncedAt)) FROM PeakRawData),
    (SELECT DATEADD(HOUR, 2, MAX(ImportedAt)) FROM StockActionEvents)";

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                status.PeakRawDataCount =
                    reader.IsDBNull(0) ? 0 : reader.GetInt32(0);

                status.StockActionEventsCount =
                    reader.IsDBNull(1) ? 0 : reader.GetInt32(1);

                status.LatestRawSyncAt =
                    reader.IsDBNull(2) ? null : reader.GetDateTime(2);

                status.LatestEventImportedAt =
                    reader.IsDBNull(3) ? null : reader.GetDateTime(3);
            }
        }
        catch
        {
            status.DatabaseOnline = false;
        }

        return status;
    }
}