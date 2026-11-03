
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

    // Verify that an existing SQL Server backup file is valid.
    public async Task<bool> VerifyBackupAsync(string backupFilePath)
    {
        // Check whether the backup file exists.
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException(
                "The backup file could not be found.",
                backupFilePath);
        }

        // SQL Server uses this command to verify a backup.
        string sql = """
            RESTORE VERIFYONLY
            FROM DISK = @BackupFile;
            """;

        // Connect to SQL Server using our existing connection string.
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        // Prepare the SQL command.
        await using var command = new SqlCommand(sql, connection);

        // Pass the backup file path safely as a parameter.
        command.Parameters.AddWithValue("@BackupFile", backupFilePath);

        // Execute the verification command.
        await command.ExecuteNonQueryAsync();

        // Return true if SQL Server completes verification successfully.
        return true;
    }  

    
    // Restore a backup into a new database without overwriting an existing one.
    public async Task RestoreBackupAsync(
        string backupFilePath,
        string newDatabaseName)
    {
        // Check whether the backup file exists.
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException(
                "The backup file could not be found.",
                backupFilePath);
        }

        // Validate the new database name.
        if (string.IsNullOrWhiteSpace(newDatabaseName) ||
            newDatabaseName.Length > 128)
        {
            throw new ArgumentException(
                "Please provide a valid database name.");
        }

        // Escape closing brackets in the database name.
        string safeDatabaseName = newDatabaseName.Replace("]", "]]");

        // Connect to SQL Server.
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        // Check whether the destination database already exists.
        string checkDatabaseSql = """
            SELECT COUNT(*)
            FROM sys.databases
            WHERE name = @DatabaseName;
            """;

        await using var checkCommand =
            new SqlCommand(checkDatabaseSql, connection);

        checkCommand.Parameters.AddWithValue(
            "@DatabaseName", newDatabaseName);

        int databaseExists = Convert.ToInt32(
            await checkCommand.ExecuteScalarAsync());

        // Never overwrite an existing database.
        if (databaseExists > 0)
        {
            throw new InvalidOperationException(
                "The destination database already exists. " +
                "Choose a different database name.");
        }

        
        // Read the logical file names stored inside the backup.
        string fileListSql = """
            RESTORE FILELISTONLY
            FROM DISK = @BackupFile;
            """;

        await using var fileListCommand =
            new SqlCommand(fileListSql, connection);

        fileListCommand.Parameters.AddWithValue(
            "@BackupFile", backupFilePath);

        // Execute the command and read the backup file information.
        await using var reader =
            await fileListCommand.ExecuteReaderAsync();

        string? dataLogicalName = null;
        string? logLogicalName = null;

        // Find the data file and transaction log names.
        while (await reader.ReadAsync())
        {
            string logicalName = reader["LogicalName"].ToString() ?? "";
            string fileType = reader["Type"].ToString() ?? "";

            if (fileType == "D" && dataLogicalName == null)
            {
                dataLogicalName = logicalName;
            }
            else if (fileType == "L" && logLogicalName == null)
            {
                logLogicalName = logicalName;
            }
        }

        // Close the reader before executing another SQL command.
        await reader.CloseAsync();

        // Make sure both logical file names were found.
        if (string.IsNullOrWhiteSpace(dataLogicalName) ||
            string.IsNullOrWhiteSpace(logLogicalName))
        {
            throw new InvalidOperationException(
                "Could not find the database data and log files in the backup.");
        }

        // Create new physical file paths for the restored database.
        string dataFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            $"{newDatabaseName}.mdf");

        string logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            $"{newDatabaseName}_log.ldf");

        // Escape file logical names for SQL Server identifiers.
        string safeDataLogicalName = dataLogicalName.Replace("'", "''");
        string safeLogLogicalName = logLogicalName.Replace("'", "''");

        // Build the restore command using the new database and file paths.
        string restoreSql = $"""
            RESTORE DATABASE [{safeDatabaseName}]
            FROM DISK = @BackupFile
            WITH
                MOVE N'{safeDataLogicalName}' TO @DataFile,
                MOVE N'{safeLogLogicalName}' TO @LogFile,
                RECOVERY,
                STATS = 10;
            """;

        // Prepare the restore command.
        await using var restoreCommand =
            new SqlCommand(restoreSql, connection);

        // Pass file paths as SQL parameters.
        restoreCommand.Parameters.AddWithValue(
            "@BackupFile", backupFilePath);

        restoreCommand.Parameters.AddWithValue(
            "@DataFile", dataFilePath);

        restoreCommand.Parameters.AddWithValue(
            "@LogFile", logFilePath);

        // Execute the restore operation.
        await restoreCommand.ExecuteNonQueryAsync();

    }
    
}
