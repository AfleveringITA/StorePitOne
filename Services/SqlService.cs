using Microsoft.Data.SqlClient;
using System.Text.Json;
using StorePitOne.Models;

public class SqlService
{
    private readonly string _connectionString;

    public SqlService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("StorePitDb")!;
    }

    public async Task TestConnection()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
    }

    public class CustomerDropdown
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class CustomerAdminViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public string ApiKeyPreview { get; set; } = "";
    }

    public class UserAdminViewModel
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class StockActionViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string? ItemNumber { get; set; }
        public string? ActionType { get; set; }
        public decimal Quantity { get; set; }
    }

    public class DbCustomer
    {
        public int Id { get; set; }
        public string ApiKey { get; set; } = "";
    }

    public async Task<List<UserAdminViewModel>> GetUsersForAdmin()
    {
        var list = new List<UserAdminViewModel>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Id, Email, Role, CreatedAt
FROM Users
ORDER BY Id DESC";

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new UserAdminViewModel
            {
                Id = reader.GetInt32(0),
                Email = reader.GetString(1),
                Role = reader.GetString(2),
                CreatedAt = reader.GetDateTime(3)
            });
        }

        return list;
    }

    public async Task CreateUser(string email, string password, string role)
    {
        if (password.Length != 6)
        {
            throw new Exception("Kodeord skal være præcis 6 tegn.");
        }

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Users
(Email, [Password], Role)
VALUES
(@Email, @Password, @Role)";

        cmd.Parameters.AddWithValue("@Email", email);
        cmd.Parameters.AddWithValue("@Password", password);
        cmd.Parameters.AddWithValue("@Role", role);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteUser(int userId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DELETE FROM Users
WHERE Id = @UserId";

        cmd.Parameters.AddWithValue("@UserId", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<CustomerDropdown>> GetCustomersForDropdown()
    {
        var list = new List<CustomerDropdown>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Customers ORDER BY Name";

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new CustomerDropdown
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return list;
    }

    public async Task<List<CustomerAdminViewModel>> GetCustomersForAdmin()
    {
        var list = new List<CustomerAdminViewModel>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
SELECT Id, Name, IsActive, ApiKey
FROM Customers
ORDER BY Id DESC";

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var apiKey = reader.IsDBNull(3) ? "" : reader.GetString(3);

            list.Add(new CustomerAdminViewModel
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                IsActive = !reader.IsDBNull(2) && reader.GetBoolean(2),
                ApiKeyPreview = MaskApiKey(apiKey)
            });
        }

        return list;
    }

    public async Task CreateCustomer(string name, string apiKey)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
INSERT INTO Customers
(Name, ApiKey, IsActive)
VALUES
(@Name, @ApiKey, 1)";

        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@ApiKey", apiKey);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeactivateCustomer(int customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
UPDATE Customers
SET IsActive = 0
WHERE Id = @CustomerId";

        cmd.Parameters.AddWithValue("@CustomerId", customerId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReactivateCustomer(int customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
UPDATE Customers
SET IsActive = 1
WHERE Id = @CustomerId";

        cmd.Parameters.AddWithValue("@CustomerId", customerId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<DbCustomer>> GetCustomers()
    {
        var customers = new List<DbCustomer>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ApiKey FROM Customers WHERE IsActive = 1";

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            customers.Add(new DbCustomer
            {
                Id = reader.GetInt32(0),
                ApiKey = reader.GetString(1)
            });
        }

        return customers;
    }

    public async Task<List<StockActionViewModel>> GetStockActionsForCustomer(
        int customerId,
        string itemNumber,
        DateTime fromDate,
        DateTime toDate)
    {
        var list = new List<StockActionViewModel>();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;

        cmd.CommandText = @"
SELECT CreatedAt, ItemNumber, ActionType, Quantity
FROM StockActionEvents
WHERE CustomerId = @CustomerId
AND CreatedAt >= @FromDate
AND CreatedAt < @ToDate
AND (@ItemNumber = '' OR ItemNumber LIKE '%' + @ItemNumber + '%')
ORDER BY CreatedAt DESC";

        cmd.Parameters.AddWithValue("@CustomerId", customerId);
        cmd.Parameters.AddWithValue("@ItemNumber", itemNumber ?? "");
        cmd.Parameters.AddWithValue("@FromDate", fromDate);
        cmd.Parameters.AddWithValue("@ToDate", toDate);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new StockActionViewModel
            {
                CreatedAt = reader.GetDateTime(0),
                ItemNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                ActionType = reader.IsDBNull(2) ? null : reader.GetString(2),
                Quantity = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)
            });
        }

        return list;
    }

    public async Task InsertStockActions(int customerId, List<PeakStockActionDto> actions)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var a in actions)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 0;

            cmd.CommandText = @"
IF NOT EXISTS (
    SELECT 1 FROM StockActions
    WHERE CustomerId = @CustomerId AND PeakId = @PeakId
)
BEGIN
    INSERT INTO StockActions
    (CustomerId, PeakId, ItemNumber, ActionType, Quantity, CreatedAt)
    VALUES
    (@CustomerId, @PeakId, @ItemNumber, @ActionType, @Quantity, @CreatedAt)
END";

            cmd.Parameters.AddWithValue("@CustomerId", customerId);
            cmd.Parameters.AddWithValue("@PeakId", a.Id);
            cmd.Parameters.AddWithValue("@ItemNumber", (object?)a.ItemNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ActionType", a.AdjustmentType.ToString());
            cmd.Parameters.AddWithValue("@Quantity", a.QuantityAdjusted);
            cmd.Parameters.AddWithValue("@CreatedAt", a.AdjustmentTime);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task InsertProducts(int customerId, List<PeakProductDto> products)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var p in products)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 0;

            cmd.CommandText = @"
IF NOT EXISTS (
    SELECT 1 FROM Products
    WHERE CustomerId = @CustomerId AND PeakId = @PeakId
)
BEGIN
    INSERT INTO Products
    (CustomerId, PeakId, Sku, Ean, Name, Description, IsActive, RawJson, SyncedAt)
    VALUES
    (@CustomerId, @PeakId, @Sku, @Ean, @Name, @Description, @IsActive, @RawJson, GETUTCDATE())
END";

            cmd.Parameters.AddWithValue("@CustomerId", customerId);
            cmd.Parameters.AddWithValue("@PeakId", p.Id);
            cmd.Parameters.AddWithValue("@Sku", (object?)p.ItemNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ean", DBNull.Value);
            cmd.Parameters.AddWithValue("@Name", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", true);
            cmd.Parameters.AddWithValue("@RawJson", JsonSerializer.Serialize(p));

            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task InsertStock(int customerId, List<PeakStockDto> stockItems)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var s in stockItems)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 0;

            cmd.CommandText = @"
IF NOT EXISTS (
    SELECT 1 FROM Stock
    WHERE CustomerId = @CustomerId AND PeakId = @PeakId
)
BEGIN
    INSERT INTO Stock
    (CustomerId, PeakId, Sku, Ean, Quantity, AvailableQuantity, ReservedQuantity, LocationCode, RawJson, SyncedAt)
    VALUES
    (@CustomerId, @PeakId, @Sku, @Ean, @Quantity, @AvailableQuantity, @ReservedQuantity, @LocationCode, @RawJson, GETUTCDATE())
END";

            cmd.Parameters.AddWithValue("@CustomerId", customerId);
            cmd.Parameters.AddWithValue("@PeakId", s.Id);
            cmd.Parameters.AddWithValue("@Sku", (object?)s.ItemNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ean", (object?)s.Ean ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Quantity", s.Quantity);
            cmd.Parameters.AddWithValue("@AvailableQuantity", s.AvailableQuantity);
            cmd.Parameters.AddWithValue("@ReservedQuantity", s.ReservedQuantity);
            cmd.Parameters.AddWithValue("@LocationCode", (object?)s.LocationCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RawJson", JsonSerializer.Serialize(s));

            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "";
        }

        if (apiKey.Length <= 8)
        {
            return "********";
        }

        return $"{apiKey[..4]}********{apiKey[^4..]}";
    }
}