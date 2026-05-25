using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace StorePitOne.Services;

public class StockActionExtractionService
{
    private readonly string _connectionString;

    private const int BatchSize = 1000;
    private const int MaxBatches = 50;

    public StockActionExtractionService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("StorePitDb")!;
    }

    public async Task ExtractStockActionsFromRawData()
    {
        Console.WriteLine("Starter BULK extraction af StockActionEvents...");

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var totalRawRows = 0;
        var totalEvents = 0;
        var totalInserted = 0;
        var totalDuplicates = 0;
        var totalErrors = 0;

        for (var batchNo = 1; batchNo <= MaxBatches; batchNo++)
        {
            var rawRows = await LoadRawRows(conn);

            if (rawRows.Count == 0)
            {
                Console.WriteLine("Ingen flere raw rows at behandle.");
                break;
            }

            Console.WriteLine($"Batch {batchNo}: Raw rows fundet: {rawRows.Count}");

            var table = CreateStageDataTable();
            var batchErrors = 0;

            foreach (var row in rawRows)
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.RawJson);

                    if (!doc.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        continue;
                    }

                    if (dataElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var item in dataElement.EnumerateArray())
                    {
                        var peakId = GetLongOrNull(item, "id");

                        if (peakId == null)
                        {
                            continue;
                        }

                        table.Rows.Add(
                            row.CustomerId,
                            row.PeakRawDataId,
                            peakId.Value,
                            (object?)GetStringOrNull(item, "itemNumber") ?? DBNull.Value,
                            (object?)(
                                GetStringOrNull(item, "adjustmentType") ??
                                GetStringOrNull(item, "actionType") ??
                                GetStringOrNull(item, "type")
                            ) ?? DBNull.Value,
                            (object?)(
                                GetDecimalOrNull(item, "quantityAdjusted") ??
                                GetDecimalOrNull(item, "quantity") ??
                                GetDecimalOrNull(item, "quantityChange")
                            ) ?? DBNull.Value,
                            (object?)(
                                GetDateTimeOrNull(item, "adjustmentTime") ??
                                GetDateTimeOrNull(item, "createdAt") ??
                                GetDateTimeOrNull(item, "updatedAt")
                            ) ?? DBNull.Value,
                            item.GetRawText()
                        );
                    }
                }
                catch (Exception ex)
                {
                    batchErrors++;
                    Console.WriteLine($"Fejl ved parsing af RawId {row.PeakRawDataId}: {ex.Message}");
                }
            }

            Console.WriteLine($"Batch {batchNo}: Events pakket ud til stage: {table.Rows.Count}");

            await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();

            try
            {
                await ClearStageTable(conn, transaction);

                if (table.Rows.Count > 0)
                {
                    await BulkInsertStage(conn, transaction, table);
                }

                var inserted = await InsertNewEventsFromStage(conn, transaction);

                await MarkRawRowsAsExtracted(
                    conn,
                    transaction,
                    rawRows.Select(x => x.PeakRawDataId).ToList());

                await transaction.CommitAsync();

                var duplicates = table.Rows.Count - inserted;

                totalRawRows += rawRows.Count;
                totalEvents += table.Rows.Count;
                totalInserted += inserted;
                totalDuplicates += duplicates;
                totalErrors += batchErrors;

                Console.WriteLine($"Batch {batchNo} færdig.");
                Console.WriteLine($"Raw rows: {rawRows.Count}");
                Console.WriteLine($"Events: {table.Rows.Count}");
                Console.WriteLine($"Nye events indsat: {inserted}");
                Console.WriteLine($"Skipped/duplicates: {duplicates}");
                Console.WriteLine($"Fejl: {batchErrors}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                Console.WriteLine("BULK extraction fejlede.");
                Console.WriteLine(ex.Message);
                break;
            }
        }

        Console.WriteLine("BULK extraction samlet færdig.");
        Console.WriteLine($"Total raw rows behandlet: {totalRawRows}");
        Console.WriteLine($"Total events fundet: {totalEvents}");
        Console.WriteLine($"Total nye events indsat: {totalInserted}");
        Console.WriteLine($"Total skipped/duplicates: {totalDuplicates}");
        Console.WriteLine($"Total fejl: {totalErrors}");
    }

    private static async Task<List<(int PeakRawDataId, int CustomerId, string RawJson)>> LoadRawRows(SqlConnection conn)
    {
        var list = new List<(int PeakRawDataId, int CustomerId, string RawJson)>();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = $@"
SELECT Id, CustomerId, RawJson
FROM PeakRawData
WHERE EndpointName = 'stockAction'
AND HttpStatusCode = 200
AND RawJson IS NOT NULL
AND IsExtracted = 0
ORDER BY Id
OFFSET 0 ROWS
FETCH NEXT {BatchSize} ROWS ONLY";

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add((
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2)
            ));
        }

        return list;
    }

    private static DataTable CreateStageDataTable()
    {
        var table = new DataTable();

        table.Columns.Add("CustomerId", typeof(int));
        table.Columns.Add("PeakRawDataId", typeof(int));
        table.Columns.Add("PeakId", typeof(long));
        table.Columns.Add("ItemNumber", typeof(string));
        table.Columns.Add("ActionType", typeof(string));
        table.Columns.Add("Quantity", typeof(decimal));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("RawJson", typeof(string));

        return table;
    }

    private static async Task ClearStageTable(SqlConnection conn, SqlTransaction transaction)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandTimeout = 0;
        cmd.CommandText = "DELETE FROM StockActionEvents_Stage";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task BulkInsertStage(SqlConnection conn, SqlTransaction transaction, DataTable table)
    {
        using var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction);

        bulkCopy.DestinationTableName = "StockActionEvents_Stage";
        bulkCopy.BulkCopyTimeout = 0;

        bulkCopy.ColumnMappings.Add("CustomerId", "CustomerId");
        bulkCopy.ColumnMappings.Add("PeakRawDataId", "PeakRawDataId");
        bulkCopy.ColumnMappings.Add("PeakId", "PeakId");
        bulkCopy.ColumnMappings.Add("ItemNumber", "ItemNumber");
        bulkCopy.ColumnMappings.Add("ActionType", "ActionType");
        bulkCopy.ColumnMappings.Add("Quantity", "Quantity");
        bulkCopy.ColumnMappings.Add("CreatedAt", "CreatedAt");
        bulkCopy.ColumnMappings.Add("RawJson", "RawJson");

        await bulkCopy.WriteToServerAsync(table);
    }

    private static async Task<int> InsertNewEventsFromStage(SqlConnection conn, SqlTransaction transaction)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
;WITH DedupedStage AS
(
    SELECT
        CustomerId,
        PeakRawDataId,
        PeakId,
        ItemNumber,
        ActionType,
        Quantity,
        CreatedAt,
        RawJson,
        ROW_NUMBER() OVER (
            PARTITION BY CustomerId, PeakId
            ORDER BY PeakRawDataId
        ) AS rn
    FROM StockActionEvents_Stage
)
INSERT INTO StockActionEvents
(CustomerId, PeakRawDataId, PeakId, ItemNumber, ActionType, Quantity, CreatedAt, RawJson, ImportedAt)
SELECT
    s.CustomerId,
    s.PeakRawDataId,
    s.PeakId,
    s.ItemNumber,
    s.ActionType,
    s.Quantity,
    s.CreatedAt,
    s.RawJson,
    GETDATE()
FROM DedupedStage s
WHERE s.rn = 1
AND NOT EXISTS
(
    SELECT 1
    FROM StockActionEvents e
    WHERE e.CustomerId = s.CustomerId
    AND e.PeakId = s.PeakId
);";

        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task MarkRawRowsAsExtracted(SqlConnection conn, SqlTransaction transaction, List<int> rawIds)
    {
        foreach (var rawId in rawIds)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandTimeout = 0;

            cmd.CommandText = @"
UPDATE PeakRawData
SET IsExtracted = 1
WHERE Id = @PeakRawDataId";

            cmd.Parameters.AddWithValue("@PeakRawDataId", rawId);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string? GetStringOrNull(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind != JsonValueKind.Null)
        {
            return property.ToString();
        }

        return null;
    }

    private static long? GetLongOrNull(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt64(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static decimal? GetDecimalOrNull(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetDecimal(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String &&
                decimal.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static DateTime? GetDateTimeOrNull(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}