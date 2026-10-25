
using DatabaseBackupUtility.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseBackupUtility.Web.Data;

public class UtilityDbContext : DbContext
{
    public UtilityDbContext(DbContextOptions<UtilityDbContext> options)
        : base(options)
    {
    }

    // Tells Entity Framework Core to manage the BackupJobs table.
    public DbSet<BackupJob> BackupJobs { get; set; } = null!;
}
