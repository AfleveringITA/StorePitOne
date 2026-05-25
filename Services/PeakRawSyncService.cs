using Microsoft.Data.SqlClient;

namespace StorePitOne.Services;

public class PeakRawSyncService
{
    private readonly string _connectionString;

    public PeakRawSyncService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("StorePitDb")!;
    }

    public async Task InsertPeakRawData(
        int customerId,
        string endpointName,
        string url,
        int httpStatusCode,
        int? totalRecords,
        string rawJson)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
INSERT INTO PeakRawData
(CustomerId, EndpointName, Url, HttpStatusCode, TotalRecords, RawJson, SyncedAt)
VALUES
(@CustomerId, @EndpointName, @Url, @HttpStatusCode, @TotalRecords, @RawJson, GETUTCDATE())";

        cmd.Parameters.AddWithValue("@CustomerId", customerId);
        cmd.Parameters.AddWithValue("@EndpointName", endpointName);
        cmd.Parameters.AddWithValue("@Url", url);
        cmd.Parameters.AddWithValue("@HttpStatusCode", httpStatusCode);
        cmd.Parameters.AddWithValue("@TotalRecords", (object?)totalRecords ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RawJson", (object?)rawJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }
}