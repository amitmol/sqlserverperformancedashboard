using System.Data.Common;
using Dashboard.Backend;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Web.Controllers;

[Authorize(Policy = "Monitoring")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SqlMonitoringDataController(DashboardReadService service, ILogger<SqlMonitoringDataController> logger) : Controller
{
    [HttpGet] public IActionResult Context() => Read("Context");
    [HttpGet] public IActionResult Overview() => Read("Overview");
    [HttpGet] public IActionResult Queries() => Read("Queries");
    [HttpGet] public IActionResult Blocking() => Read("Blocking");
    [HttpGet] public IActionResult Waits() => Read("Waits");
    [HttpGet] public IActionResult FileIo() => Read("FileIo");
    [HttpGet] public IActionResult History() => Read("History");
    [HttpGet] public IActionResult Samples() => Read("Samples");
    private IActionResult Read(string action)
    {
        try
        {
            if (Request.Query.Count > 20 || Request.Query.Any(q => q.Value.Count != 1))
                throw new DashboardException(400, "invalidFilter", "Duplicate or excessive filters are not supported.");
            var query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
            var grants = User.FindAll("monitoring-instance").Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToHashSet();
            return Json(service.Read(action, query, grants));
        }
        catch (DashboardException ex) { return Error(ex.StatusCode, ex.Code, ex.Message); }
        catch (DbException)
        {
            logger.LogWarning("Monitoring storage read failed. Request {RequestId}", HttpContext.TraceIdentifier);
            return Error(503, "storageUnavailable", "Local snapshot storage is unavailable. Start LocalDB and run the development setup script.");
        }
        catch (Exception)
        {
            logger.LogError("Monitoring request failed. Request {RequestId}", HttpContext.TraceIdentifier);
            return Error(500, "readFailed", "The snapshot request could not be completed.");
        }
    }
    private ObjectResult Error(int status, string code, string message) => StatusCode(status,
        new { contractVersion = 1, requestId = HttpContext.TraceIdentifier, generatedAtUtc = DateTime.UtcNow, error = new { code, message } });
}
