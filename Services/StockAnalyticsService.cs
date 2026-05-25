using Microsoft.Data.SqlClient;

namespace StorePitOne.Services;

public class StockAnalyticsService
{
    private readonly string _connectionString;

    public StockAnalyticsService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("StorePitDb")!;
    }

    public class ProductMovementSummary
    {
        public int CustomerId { get; set; }
        public string? ItemNumber { get; set; }
        public DateTime? LastMovementAt { get; set; }
        public int DaysSinceLastMovement { get; set; }
        public int MovementCount { get; set; }
    }

    public class DeadStockItem
    {
        public int CustomerId { get; set; }
        public string? ItemNumber { get; set; }
        public DateTime? LastMovementAt { get; set; }
        public int DaysSinceLastMovement { get; set; }
        public int MovementCount { get; set; }
    }

    public class SlowMoverItem
    {
        public int CustomerId { get; set; }
        public string? ItemNumber { get; set; }
        public DateTime? FirstMovementAt { get; set; }
        public DateTime? LastMovementAt { get; set; }
        public int MovementCount { get; set; }
        public int PeriodDays { get; set; }
    }

    public class ItemHistoryEvent
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? ItemNumber { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? ActionType { get; set; }
        public decimal Quantity { get; set; }
    }

    public class StockStatusItem
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string ItemNumber { get; set; } = "";

        public DateTime? LastInboundAt { get; set; }
        public decimal LastInboundQuantity { get; set; }

        public decimal QuantityOnStock { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }

        public decimal OutboundSinceLastInbound { get; set; }
        public int DaysSinceLastInbound { get; set; }

        public decimal PricePerDay { get; set; }
        public decimal EstimatedStoragePrice { get; set; }

        public string DeadStockRisk { get; set; } = "";
    }

    public async Task<List<ProductMovementSummary>> GetProductMovementSummary()
    {
        var list = new List<ProductMovementSummary>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
SELECT
    CustomerId,
    ItemNumber,
    MAX(CreatedAt) AS LastMovementAt,
    DATEDIFF(DAY, MAX(CreatedAt), GETUTCDATE()) AS DaysSinceLastMovement,
    COUNT(*) AS MovementCount
FROM StockActionEvents
WHERE ItemNumber IS NOT NULL
GROUP BY CustomerId, ItemNumber
ORDER BY DaysSinceLastMovement DESC";

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new ProductMovementSummary
            {
                CustomerId = reader.GetInt32(0),
                ItemNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                LastMovementAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                DaysSinceLastMovement = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                MovementCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
            });
        }

        return list;
    }

    public async Task<List<DeadStockItem>> GetDeadStockItems(int minimumDaysWithoutMovement = 90)
    {
        var list = new List<DeadStockItem>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
SELECT
    CustomerId,
    ItemNumber,
    MAX(CreatedAt) AS LastMovementAt,
    DATEDIFF(DAY, MAX(CreatedAt), GETUTCDATE()) AS DaysSinceLastMovement,
    COUNT(*) AS MovementCount
FROM StockActionEvents
WHERE ItemNumber IS NOT NULL
GROUP BY CustomerId, ItemNumber
HAVING DATEDIFF(DAY, MAX(CreatedAt), GETUTCDATE()) >= @MinimumDaysWithoutMovement
ORDER BY DaysSinceLastMovement DESC";

        cmd.Parameters.AddWithValue("@MinimumDaysWithoutMovement", minimumDaysWithoutMovement);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new DeadStockItem
            {
                CustomerId = reader.GetInt32(0),
                ItemNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                LastMovementAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                DaysSinceLastMovement = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                MovementCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
            });
        }

        return list;
    }

    public async Task<List<SlowMoverItem>> GetSlowMoverItems(
        int periodDays = 90,
        int maximumMovements = 3)
    {
        var list = new List<SlowMoverItem>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
SELECT
    CustomerId,
    ItemNumber,
    MIN(CreatedAt) AS FirstMovementAt,
    MAX(CreatedAt) AS LastMovementAt,
    COUNT(*) AS MovementCount
FROM StockActionEvents
WHERE ItemNumber IS NOT NULL
AND CreatedAt >= DATEADD(DAY, -@PeriodDays, GETUTCDATE())
GROUP BY CustomerId, ItemNumber
HAVING COUNT(*) <= @MaximumMovements
ORDER BY MovementCount ASC, LastMovementAt ASC";

        cmd.Parameters.AddWithValue("@PeriodDays", periodDays);
        cmd.Parameters.AddWithValue("@MaximumMovements", maximumMovements);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new SlowMoverItem
            {
                CustomerId = reader.GetInt32(0),
                ItemNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                FirstMovementAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                LastMovementAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                MovementCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                PeriodDays = periodDays
            });
        }

        return list;
    }

    public async Task<List<ItemHistoryEvent>> GetItemHistory(
        string itemNumber,
        int pageNumber = 1,
        int pageSize = 500)
    {
        var list = new List<ItemHistoryEvent>();

        if (string.IsNullOrWhiteSpace(itemNumber))
        {
            return list;
        }

        var offset = (pageNumber - 1) * pageSize;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
SELECT
    sae.CustomerId,
    c.Name AS CustomerName,
    sae.ItemNumber,
    sae.CreatedAt,
    sae.ActionType,
    sae.Quantity
FROM StockActionEvents sae
LEFT JOIN Customers c
    ON sae.CustomerId = c.Id
WHERE sae.ItemNumber = @ItemNumber
ORDER BY sae.CreatedAt DESC
OFFSET @Offset ROWS
FETCH NEXT @PageSize ROWS ONLY";

        cmd.Parameters.AddWithValue("@ItemNumber", itemNumber.Trim());
        cmd.Parameters.AddWithValue("@Offset", offset);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new ItemHistoryEvent
            {
                CustomerId = reader.GetInt32(0),
                CustomerName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ItemNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                ActionType = reader.IsDBNull(4) ? null : reader.GetString(4),
                Quantity = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
            });
        }

        return list;
    }

    public async Task<int> GetItemHistoryCount(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber))
        {
            return 0;
        }

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
SELECT COUNT(*)
FROM StockActionEvents
WHERE ItemNumber = @ItemNumber";

        cmd.Parameters.AddWithValue("@ItemNumber", itemNumber.Trim());

        var result = await cmd.ExecuteScalarAsync();

        return result == null || result == DBNull.Value
            ? 0
            : Convert.ToInt32(result);
    }

    public async Task<StockStatusItem?> GetStockStatusForItem(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber))
        {
            return null;
        }

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
SELECT TOP 1
    inbound.CustomerId,
    c.Name AS CustomerName,
    inbound.ItemNumber,
    inbound.CreatedAt AS LastInboundAt,
    inbound.Quantity AS LastInboundQuantity,
    ISNULL(outbound.OutboundSinceLastInbound, 0) AS OutboundSinceLastInbound
FROM StockActionEvents inbound
LEFT JOIN Customers c
    ON inbound.CustomerId = c.Id
OUTER APPLY
(
    SELECT SUM(ABS(x.Quantity)) AS OutboundSinceLastInbound
    FROM StockActionEvents x
    WHERE x.ItemNumber = inbound.ItemNumber
    AND x.CustomerId = inbound.CustomerId
    AND x.ActionType = '300'
    AND x.CreatedAt >= inbound.CreatedAt
) outbound
WHERE inbound.ItemNumber = @ItemNumber
AND inbound.ActionType = '400'
ORDER BY inbound.CreatedAt DESC";

        cmd.Parameters.AddWithValue("@ItemNumber", itemNumber.Trim());

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var lastInboundAt = reader.IsDBNull(3)
                ? (DateTime?)null
                : reader.GetDateTime(3);

            var inboundQuantity = reader.IsDBNull(4)
                ? 0
                : reader.GetDecimal(4);

            var outboundSinceLastInbound = reader.IsDBNull(5)
                ? 0
                : reader.GetDecimal(5);

            var quantityOnStock = inboundQuantity - outboundSinceLastInbound;

            if (quantityOnStock < 0)
            {
                quantityOnStock = 0;
            }

            var daysSinceLastInbound = lastInboundAt == null
                ? 0
                : (DateTime.UtcNow - lastInboundAt.Value).Days;

            return new StockStatusItem
            {
                CustomerId = reader.GetInt32(0),
                CustomerName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ItemNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),

                LastInboundAt = lastInboundAt,
                LastInboundQuantity = inboundQuantity,

                QuantityOnStock = quantityOnStock,
                AvailableQuantity = quantityOnStock,
                ReservedQuantity = 0,

                OutboundSinceLastInbound = outboundSinceLastInbound,
                DaysSinceLastInbound = daysSinceLastInbound,

                PricePerDay = 0,
                EstimatedStoragePrice = 0,

                DeadStockRisk = daysSinceLastInbound > 90
                    ? "DØD"
                    : daysSinceLastInbound > 30
                        ? "LANGSOM"
                        : "AKTIV"
            };
        }

        return null;
    }
}