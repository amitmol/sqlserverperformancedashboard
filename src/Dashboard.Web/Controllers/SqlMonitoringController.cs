using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Web.Controllers;

[Authorize(Policy = "Monitoring")]
public sealed class SqlMonitoringController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
