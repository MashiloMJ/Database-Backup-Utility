
using Microsoft.EntityFrameworkCore;

namespace DatabaseBackupUtility.Web.Data;

public class UtilityDbContext : DbContext
{
    public UtilityDbContext(DbContextOptions<UtilityDbContext> options)
        : base(options)
    {
    }
}
