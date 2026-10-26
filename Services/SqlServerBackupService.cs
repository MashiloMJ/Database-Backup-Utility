
using Microsoft.Data.SqlClient;

namespace DatabaseBackupUtility.Web.Services;

// This service creates backup files for SQL Server databases.
public class SqlServerBackupService
{
    // Store the connection string configured in appsettings.json.
    private readonly string _connectionString;

    // ASP.NET Core provides the application configuration.
    public SqlServerBackupService(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "The DefaultConnection connection string is missing.");
    }

    // Create a backup file and return its full path.
    public async Task<string> CreateBackupAsync(
        string databaseName,
        string backupFolder)
    {
        // Validate the database name before building the SQL command.
        if (string.IsNullOrWhiteSpace(databaseName) ||
            databaseName.Length > 128)
        {
            throw new ArgumentException(
                "Please provide a valid database name.");
        }

        // Create the folder if it does not already exist.
        Directory.CreateDirectory(backupFolder);

        // Add a timestamp so each backup has a unique filename.
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string backupFile = Path.Combine(
            backupFolder,
            $"{databaseName}_{timestamp}.bak");

        // Escape closing brackets in the SQL Server database identifier.
        string safeDatabaseName = databaseName.Replace("]", "]]");

        
        // Create a backup without compression because SQL Server Express
        // does not support the COMPRESSION backup option.
        string sql = $"""
            BACKUP DATABASE [{safeDatabaseName}]
            TO DISK = @BackupFile
            WITH INIT, STATS = 10;
            """;


        // Connect using the connection string from appsettings.json.
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        // Prepare the backup command.
        await using var command = new SqlCommand(sql, connection);

        // Pass the file path as a SQL parameter.
        command.Parameters.AddWithValue("@BackupFile", backupFile);

        // Ask SQL Server to create the backup file.
        await command.ExecuteNonQueryAsync();

        // Return the path so the controller can record it.
        return backupFile;
    }
}
