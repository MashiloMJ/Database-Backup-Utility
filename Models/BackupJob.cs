
namespace DatabaseBackupUtility.Web.Models;

// This class represents one database backup job.
public class BackupJob
{
    // Unique ID for each backup job.
    public int Id { get; set; }

    // Name of the database we want to back up.
    public string DatabaseName { get; set; } = string.Empty;

    // When the backup job was created.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Current status: Pending, Running, Completed, or Failed.
    public string Status { get; set; } = "Pending";

    // Location of the backup file, once it has been created.
    public string? BackupFilePath { get; set; }

    // Error details if the backup fails.
    public string? ErrorMessage { get; set; }
}
