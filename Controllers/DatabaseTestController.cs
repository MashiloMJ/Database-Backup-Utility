
using DatabaseBackupUtility.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseBackupUtility.Web.Controllers;

public class DatabaseTestController : Controller
{
    private readonly UtilityDbContext _context;

    public DatabaseTestController(UtilityDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            bool connected = await _context.Database.CanConnectAsync();

            ViewBag.ConnectionStatus = connected
                ? "Successfully connected to SQL Server!"
                : "Could not connect to the database.";

            ViewBag.IsConnected = connected;
        }
        catch (Exception ex)
        {
            ViewBag.ConnectionStatus = "Connection failed: " + ex.Message;
            ViewBag.IsConnected = false;
        }

        return View();
    }
}
