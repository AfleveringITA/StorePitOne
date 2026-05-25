using Microsoft.Data.SqlClient;
using StorePitOne.Models;

namespace StorePitOne.Services
{
    public class UserService
    {
        private readonly IConfiguration _config;

        public AppUser? CurrentUser { get; private set; }

        public UserService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            using var conn = new SqlConnection(
                _config.GetConnectionString("StorePitDb"));

            await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT Email, Role
FROM Users
WHERE Email = @Email
AND Password = @Password";

            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Password", password);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                CurrentUser = new AppUser
                {
                    Email = reader.GetString(0),
                    Role = reader.GetString(1)
                };

                return true;
            }

            return false;
        }

        public void Logout()
        {
            CurrentUser = null;
        }
    }
}