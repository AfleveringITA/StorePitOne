using Microsoft.Data.SqlClient;

namespace StorePitOne.Services;

public class SystemSettingsService
{
    private readonly string _connectionString;

    public SystemSettingsService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("StorePitDb")!;
    }

    public class SystemSettings
    {
        public bool NightlyUpdateEnabled { get; set; }

        public TimeSpan NightlyUpdateTime { get; set; }

        public DateTime? LastNightlyRunAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public async Task<SystemSettings> GetSettings()
    {
        using var conn = new SqlConnection(_connectionString);

        await conn.OpenAsync();

        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
SELECT
    NightlyUpdateEnabled,
    NightlyUpdateTime,
    LastNightlyRunAt,
    UpdatedAt
FROM SystemSettings
WHERE Id = 1";

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new SystemSettings
            {
                NightlyUpdateEnabled = reader.GetBoolean(0),

                NightlyUpdateTime = reader.IsDBNull(1)
                    ? new TimeSpan(2, 0, 0)
                    : reader.GetTimeSpan(1),

                LastNightlyRunAt = reader.IsDBNull(2)
                    ? null
                    : reader.GetDateTime(2),

                UpdatedAt = reader.GetDateTime(3)
            };
        }

        return new SystemSettings();
    }

    public async Task UpdateNightlySettings(
        bool enabled,
        TimeSpan nightlyTime)
    {
        using var conn = new SqlConnection(_connectionString);

        await conn.OpenAsync();

        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
UPDATE SystemSettings
SET
    NightlyUpdateEnabled = @Enabled,
    NightlyUpdateTime = @NightlyTime,
    UpdatedAt = GETDATE()
WHERE Id = 1";

        cmd.Parameters.AddWithValue("@Enabled", enabled);

        cmd.Parameters.AddWithValue("@NightlyTime", nightlyTime);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkNightlyRunCompleted()
    {
        using var conn = new SqlConnection(_connectionString);

        await conn.OpenAsync();

        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
UPDATE SystemSettings
SET
    LastNightlyRunAt = DATEADD(HOUR, 2, GETDATE()),
    UpdatedAt = DATEADD(HOUR, 2, GETDATE())
WHERE Id = 1";

        await cmd.ExecuteNonQueryAsync();
    }
}