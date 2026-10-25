
using DatabaseBackupUtility.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseBackupUtility.Web.Controllers;

// Handles web requests related to backup jobs.
public class BackupJobsController : Controller
{
    // Gives this controller access to our application database.
    private readonly UtilityDbContext _context;

    // ASP.NET Core provides the database context automatically.
    public BackupJobsController(UtilityDbContext context)
    {
        _context = context;
    }

    // Displays all backup jobs saved in SQL Server.
    public async Task<IActionResult> Index()
    {
        // Read backup jobs from the database.
        // AsNoTracking is suitable because this page only displays data.
        var jobs = await _context.BackupJobs
            .AsNoTracking()
            .OrderByDescending(job => job.CreatedAt)
            .ToListAsync();

        // Send the results to the Index view.
        return View(jobs);
    }

    //---------------------form----------------------------
    
    // Displays the form for creating a backup job.
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // Receives the form data and saves a new job in SQL Server.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string databaseName)
    {
        // Remove extra spaces from the submitted database name.
        databaseName = databaseName?.Trim() ?? string.Empty;

        // Do not save a job if the database name is empty.
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            ModelState.AddModelError(
                "DatabaseName",
                "Please enter a database name.");

            return View();
        }

        // Create a new job record in memory.
        var job = new DatabaseBackupUtility.Web.Models.BackupJob
        {
            DatabaseName = databaseName,
            Status = "Pending"
        };

        // Add the job to Entity Framework Core's change tracker.
        _context.BackupJobs.Add(job);

        // Save the new record to the actual SQL Server database.
        await _context.SaveChangesAsync();

        // Return to the list of backup jobs after saving.
        return RedirectToAction(nameof(Index));
    }

}
