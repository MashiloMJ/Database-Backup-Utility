
using Microsoft.Data.SqlClient;

namespace DatabaseBackupUtility.Web.Services;

// This service handles SQL Server database backups.
public class SqlServerBackupService
{
    // Creates a backup file for the specified SQL Server database.
    public async Task<string> CreateBackupAsync(
        string databaseName,
        string backupFolder)
    {
        // Create the folder if it does not exist.
        Directory.CreateDirectory(backupFolder);

        // Create a unique filename using the current date and time.
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupFile = Path.Combine(
            backupFolder,
            $"{databaseName}_{timestamp}.bak");

        // Use parameters to safely handle the database name.
        // SQL Server does not allow parameters for BACKUP DATABASE identifiers,
        // so validate the name and quote it as a SQL identifier.
        if (string.IsNullOrWhiteSpace(databaseName) ||
            databaseName.Length > 128 ||
            databaseName.Contains(']') ||
            databaseName.Contains(';'))
        {
            throw new ArgumentException("Invalid database name.");
        }

        // SQL Server executes this command to create the backup.
        string sql = $"""
            BACKUP DATABASE [{databaseName}]
            TO DISK = @BackupFile
            WITH INIT, COMPRESSION, STATS = 10;
            """;

        // This service currently uses the local SQL Server instance.
        string connectionString =
            @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True;";

        await using var connection =
            new SqlConnection(connectionString);

        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);

        // Pass the destination path as a parameter.
        command.Parameters.AddWithValue("@BackupFile", backupFile);

        // Execute the backup command.
        await command.ExecuteNonQueryAsync();

        // Return the path so the controller can save it in the job record.
        return backupFile;
    }
}
