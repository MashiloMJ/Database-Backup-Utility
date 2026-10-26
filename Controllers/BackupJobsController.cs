
using DatabaseBackupUtility.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseBackupUtility.Web.Services;

namespace DatabaseBackupUtility.Web.Controllers;

// Handles web requests related to backup jobs.
public class BackupJobsController : Controller
{
    // Gives this controller access to our application database.
    private readonly UtilityDbContext _context;

    // Handles the actual SQL Server backup operation.
    private readonly SqlServerBackupService _backupService;

    // ASP.NET Core provides the database context automatically.
    public BackupJobsController(UtilityDbContext context,
        SqlServerBackupService backupService)
    {
        _context = context;
        _backupService = backupService;
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


    // Runs a backup for an existing job.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunBackup(int id)
    {
        // Find the selected job in the database.
        var job = await _context.BackupJobs.FindAsync(id);

        // Return to the list if the job does not exist.
        if (job == null)
        {
            return NotFound();
        }

        // Prevent a second backup attempt while a job is marked Running.
        if (job.Status == "Running")
        {
            return RedirectToAction(nameof(Index));
        }

        // Mark the job as running before starting the operation.
        job.Status = "Running";
        job.ErrorMessage = null;
        await _context.SaveChangesAsync();

        try
        {
            // Store backup files in a folder inside the application directory.
            string backupFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Backups");

            
            // Check that the backup service was provided by ASP.NET Core.
            if (_backupService == null)
            {
                throw new InvalidOperationException(
                    "The SQL Server backup service was not initialized.");
            }

            // Check that the job has a database name.
            if (string.IsNullOrWhiteSpace(job.DatabaseName))
            {
                throw new InvalidOperationException(
                    "The backup job does not have a database name.");
            }

            // Create the real SQL Server backup file.
            string backupPath = await _backupService.CreateBackupAsync(
                job.DatabaseName,
                backupFolder);


            // Record the successful backup.
            job.Status = "Completed";
            job.BackupFilePath = backupPath;
            job.ErrorMessage = null;
        }
        catch (Exception ex)
        {
            // Mark the job as failed.
            job.Status = "Failed";

            // Save the full error, including the location in the code
            // where the exception occurred.
            job.ErrorMessage = ex.ToString();
        }       

        // Save the final status and any path or error message.
        await _context.SaveChangesAsync();

        // Return to the jobs list to display the result.
        return RedirectToAction(nameof(Index));
    }


}
